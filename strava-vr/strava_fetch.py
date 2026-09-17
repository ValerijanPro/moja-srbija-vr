#!/usr/bin/env python3
"""
Faza 1: povlacenje svih Strava aktivnosti u GeoJSON za viewer / Unity.

Koraci (jednokratno):
  1. Napravi API aplikaciju na https://www.strava.com/settings/api
     (Authorization Callback Domain: localhost)
  2. Prepisi .env.example u .env i upisi STRAVA_CLIENT_ID i STRAVA_CLIENT_SECRET
  3. Pokreni:  python3 strava_fetch.py
     - skripta ispise URL za autorizaciju; otvori ga u browseru, klikni Authorize
     - browser te prebaci na http://localhost:8723/?code=XXXX (stranica "ne radi" - to je ok)
     - iskopiraj ceo taj URL (ili samo code=... deo) i nalepi u terminal
  4. Rezultat: viewer/data/routes.geojson (sve rute, dekodovani polyline-i)

Opciono:  python3 strava_fetch.py --streams 30
  povlaci detaljne streamove (visina, brzina, puls) za 30 najskorijih aktivnosti
  u viewer/data/streams/<id>.json  (pazi na Strava rate limit: 100 req / 15 min)

Bez ikakvih pip zavisnosti - cist stdlib.
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

BASE = "https://www.strava.com/api/v3"
HERE = os.path.dirname(os.path.abspath(__file__))
TOKEN_FILE = os.path.join(HERE, "token.json")
OUT_GEOJSON = os.path.join(HERE, "viewer", "data", "routes.geojson")
STREAMS_DIR = os.path.join(HERE, "viewer", "data", "streams")
REDIRECT_URI = "http://localhost:8723"


def load_env():
    env_path = os.path.join(HERE, ".env")
    if os.path.exists(env_path):
        for line in open(env_path, encoding="utf-8"):
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, _, v = line.partition("=")
                os.environ.setdefault(k.strip(), v.strip())
    cid = os.environ.get("STRAVA_CLIENT_ID")
    sec = os.environ.get("STRAVA_CLIENT_SECRET")
    if not cid or not sec:
        sys.exit("Nedostaju STRAVA_CLIENT_ID / STRAVA_CLIENT_SECRET (vidi .env.example)")
    return cid, sec


def http_json(url, data=None, token=None):
    req = urllib.request.Request(url)
    if data is not None:
        req.data = urllib.parse.urlencode(data).encode()
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req) as r:
        return json.load(r)


def authorize(cid, sec):
    params = urllib.parse.urlencode({
        "client_id": cid,
        "redirect_uri": REDIRECT_URI,
        "response_type": "code",
        "approval_prompt": "auto",
        "scope": "activity:read_all",
    })
    print("\nOtvori ovaj URL u browseru i klikni 'Authorize':\n")
    print(f"  https://www.strava.com/oauth/authorize?{params}\n")
    print("Browser ce te prebaciti na localhost URL koji 'ne radi' - to je normalno.")
    pasted = input("Nalepi ceo taj URL (ili samo vrednost code parametra): ").strip()
    if "code=" in pasted:
        code = urllib.parse.parse_qs(urllib.parse.urlparse(pasted).query)["code"][0]
    else:
        code = pasted
    tok = http_json("https://www.strava.com/oauth/token", data={
        "client_id": cid, "client_secret": sec,
        "code": code, "grant_type": "authorization_code",
    })
    json.dump(tok, open(TOKEN_FILE, "w", encoding="utf-8"))
    return tok


def get_token(cid, sec):
    if os.path.exists(TOKEN_FILE):
        tok = json.load(open(TOKEN_FILE, encoding="utf-8"))
        if tok.get("expires_at", 0) > time.time() + 60:
            return tok["access_token"]
        tok = http_json("https://www.strava.com/oauth/token", data={
            "client_id": cid, "client_secret": sec,
            "grant_type": "refresh_token",
            "refresh_token": tok["refresh_token"],
        })
        json.dump(tok, open(TOKEN_FILE, "w", encoding="utf-8"))
        return tok["access_token"]
    return authorize(cid, sec)["access_token"]


def decode_polyline(s, precision=5):
    """Google encoded polyline -> [[lon, lat], ...] (GeoJSON redosled)."""
    coords, index, lat, lng = [], 0, 0, 0
    factor = 10 ** precision
    while index < len(s):
        for is_lng in (False, True):
            shift = result = 0
            while True:
                b = ord(s[index]) - 63
                index += 1
                result |= (b & 0x1F) << shift
                shift += 5
                if b < 0x20:
                    break
            delta = ~(result >> 1) if result & 1 else result >> 1
            if is_lng:
                lng += delta
            else:
                lat += delta
        coords.append([lng / factor, lat / factor])
    return coords


def fetch_all_activities(token):
    acts, page = [], 1
    while True:
        batch = http_json(f"{BASE}/athlete/activities?per_page=200&page={page}", token=token)
        if not batch:
            break
        acts.extend(batch)
        print(f"  strana {page}: {len(batch)} aktivnosti (ukupno {len(acts)})")
        page += 1
    return acts


def activities_to_geojson(acts):
    features = []
    for a in acts:
        poly = (a.get("map") or {}).get("summary_polyline")
        if not poly:
            continue
        features.append({
            "type": "Feature",
            "geometry": {"type": "LineString", "coordinates": decode_polyline(poly)},
            "properties": {
                "id": a["id"],
                "name": a.get("name", ""),
                "sport": a.get("sport_type") or a.get("type", ""),
                "date": (a.get("start_date_local") or "")[:10],
                "distance_km": round((a.get("distance") or 0) / 1000, 2),
                "moving_time_s": a.get("moving_time", 0),
                "elev_gain_m": round(a.get("total_elevation_gain") or 0),
                "avg_speed_kmh": round((a.get("average_speed") or 0) * 3.6, 1),
                "avg_hr": a.get("average_heartrate"),
            },
        })
    return {"type": "FeatureCollection", "features": features}


def fetch_streams(token, acts, n):
    os.makedirs(STREAMS_DIR, exist_ok=True)
    keys = "latlng,altitude,time,velocity_smooth,heartrate,distance"
    done = 0
    for a in acts:
        if done >= n:
            break
        if not (a.get("map") or {}).get("summary_polyline"):
            continue
        out = os.path.join(STREAMS_DIR, f"{a['id']}.json")
        if os.path.exists(out):
            done += 1
            continue
        try:
            s = http_json(f"{BASE}/activities/{a['id']}/streams?keys={keys}&key_by_type=true",
                          token=token)
        except urllib.error.HTTPError as e:
            if e.code == 429:
                print("Rate limit (100 req / 15 min) - nastavi kasnije, vec skinuto se preskace.")
                return
            raise
        json.dump(s, open(out, "w", encoding="utf-8"))
        done += 1
        print(f"  streams {done}/{n}: {a.get('name','')} ({a['id']})")
        time.sleep(0.4)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--streams", type=int, default=0, metavar="N",
                    help="povuci detaljne streamove za N najskorijih aktivnosti")
    args = ap.parse_args()

    cid, sec = load_env()
    token = get_token(cid, sec)

    print("Povlacim listu aktivnosti...")
    acts = fetch_all_activities(token)
    gj = activities_to_geojson(acts)
    os.makedirs(os.path.dirname(OUT_GEOJSON), exist_ok=True)
    json.dump(gj, open(OUT_GEOJSON, "w", encoding="utf-8"))
    print(f"\nUpisano {len(gj['features'])} ruta u {OUT_GEOJSON}")

    if args.streams:
        print(f"\nPovlacim streamove za {args.streams} aktivnosti...")
        fetch_streams(token, acts, args.streams)

    print("\nGotovo. Pokreni viewer:  python3 -m http.server 8791  (iz strava-vr/viewer)")


if __name__ == "__main__":
    main()
