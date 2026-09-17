#!/usr/bin/env python3
"""
Generise demo rute (dok ne povezes svoj Strava nalog) tako sto rutira
izmedju realnih tacaka u Srbiji preko javnog OSRM servera, pa geometrija
prati prave puteve. Rezultat: viewer/data/routes.geojson u istom formatu
koji pravi strava_fetch.py.
"""

import json
import os
import random
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "viewer", "data", "routes.geojson")

# (ime, sport, [(lon, lat) tacke], datum)
DEMO_ROUTES = [
    ("Ada krug", "Run", [(20.4046, 44.7896), (20.3899, 44.7833), (20.4102, 44.7788), (20.4046, 44.7896)], "2026-08-24"),
    ("Kej: Kalemegdan - Zemun", "Run", [(20.4489, 44.8230), (20.4113, 44.8437), (20.3742, 44.8462)], "2026-08-17"),
    ("Kosutnjak trail", "Run", [(20.4310, 44.7756), (20.4173, 44.7648), (20.4415, 44.7605), (20.4310, 44.7756)], "2026-08-10"),
    ("Avala uspon", "Ride", [(20.4670, 44.7443), (20.5145, 44.6890)], "2026-08-08"),
    ("Beograd - Pancevacki most krug", "Ride", [(20.4489, 44.8230), (20.5316, 44.8250), (20.4820, 44.7970), (20.4489, 44.8230)], "2026-07-26"),
    ("NS kej + Petrovaradin", "Run", [(19.8451, 45.2551), (19.8615, 45.2517), (19.8710, 45.2440)], "2026-07-19"),
    ("Fruska gora: Iriski venac", "Ride", [(19.8451, 45.2551), (19.8510, 45.1560), (19.8020, 45.1220)], "2026-07-12"),
    ("Smederevski put", "Ride", [(20.4670, 44.7443), (20.6440, 44.6640), (20.9270, 44.6630)], "2026-06-28"),
    ("Zlatibor: Tornik", "Ride", [(19.7000, 43.7290), (19.6740, 43.6560)], "2026-06-14"),
    ("Ibarska ka Kosmaju", "Ride", [(20.4200, 44.7300), (20.4620, 44.6300), (20.5560, 44.4670)], "2026-05-31"),
    ("Dunavska ruta: NS - Beograd", "Ride", [(19.8615, 45.2517), (20.1030, 45.1520), (20.2960, 44.9600), (20.4113, 44.8437)], "2026-05-17"),
    ("Vrsacki breg", "Ride", [(21.2960, 45.1180), (21.3540, 45.1220)], "2026-05-03"),
]


def osrm_route(points, profile):
    coords = ";".join(f"{lon:.5f},{lat:.5f}" for lon, lat in points)
    url = (f"https://router.project-osrm.org/route/v1/{profile}/{coords}"
           f"?overview=full&geometries=geojson")
    with urllib.request.urlopen(url, timeout=20) as r:
        data = json.load(r)
    if data.get("code") != "Ok":
        raise RuntimeError(data.get("code"))
    route = data["routes"][0]
    return route["geometry"]["coordinates"], route["distance"] / 1000


def main():
    random.seed(7)
    features = []
    for i, (name, sport, pts, date) in enumerate(DEMO_ROUTES):
        profile = "foot" if sport == "Run" else "cycling"
        try:
            coords, dist_km = osrm_route(pts, profile)
        except Exception as e:
            print(f"  ! preskacem '{name}': {e}")
            continue
        speed = random.uniform(10.5, 12.5) if sport == "Run" else random.uniform(24, 30)
        features.append({
            "type": "Feature",
            "geometry": {"type": "LineString", "coordinates": coords},
            "properties": {
                "id": 1000 + i,
                "name": name,
                "sport": sport,
                "date": date,
                "distance_km": round(dist_km, 2),
                "moving_time_s": int(dist_km / speed * 3600),
                "elev_gain_m": random.randint(40, 900),
                "avg_speed_kmh": round(speed, 1),
                "avg_hr": random.randint(128, 162),
                "demo": True,
            },
        })
        print(f"  + {name}: {dist_km:.1f} km ({len(coords)} tacaka)")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump({"type": "FeatureCollection", "features": features}, open(OUT, "w"))
    print(f"\nUpisano {len(features)} demo ruta u {OUT}")


if __name__ == "__main__":
    main()
