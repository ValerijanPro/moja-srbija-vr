"""Personalizovani model tempa: brzina (km/h) po nagibu (%), iz Strava streamova."""
import json, os, glob, statistics

HERE = os.path.dirname(os.path.abspath(__file__))
routes = json.load(open(os.path.join(HERE, 'viewer/data/routes.geojson'), encoding='utf-8'))
sport_of = {str(f['properties']['id']): f['properties']['sport'] for f in routes['features']}

bins = {'Ride': {}, 'Run': {}}
used = {'Ride': 0, 'Run': 0}

for path in glob.glob(os.path.join(HERE, 'viewer/data/streams/*.json')):
    aid = os.path.splitext(os.path.basename(path))[0]
    sport = sport_of.get(aid)
    if sport not in bins:
        continue
    s = json.load(open(path, encoding='utf-8'))
    v = (s.get('velocity_smooth') or {}).get('data')
    alt = (s.get('altitude') or {}).get('data')
    dist = (s.get('distance') or {}).get('data')
    if not v or not alt or not dist or len(v) != len(alt) or len(v) != len(dist):
        continue
    used[sport] += 1
    n = len(v)
    # zagladi visinu (prozor +-8 uzoraka)
    alt_s = [statistics.fmean(alt[max(0, i-8):min(n, i+9)]) for i in range(n)]
    j = 0
    for i in range(n):
        # prozor od ~80 m unapred
        while j < n - 1 and dist[j] - dist[i] < 80:
            j += 1
        gap = dist[j] - dist[i]
        if gap < 40 or j <= i:
            continue
        grade = (alt_s[j] - alt_s[i]) / gap * 100
        spd = statistics.fmean(v[i:j+1]) * 3.6
        lo, hi = (4, 70) if sport == 'Ride' else (3, 25)
        if not (lo <= spd <= hi) or abs(grade) > 14:
            continue
        b = round(grade)
        bins[sport].setdefault(b, []).append(spd)

PACE = {}
for sport, bb in bins.items():
    min_n = 400 if sport == 'Ride' else 150
    keys = sorted(k for k, vals in bb.items() if len(vals) >= min_n)
    if not keys:
        continue
    med = {k: statistics.median(bb[k]) for k in keys}
    # blago zagladi krivu (3-bin prosek)
    sm = []
    for idx, k in enumerate(keys):
        neigh = [med[q] for q in keys[max(0, idx-1):idx+2]]
        sm.append(round(statistics.fmean(neigh), 1))
    PACE[sport] = {'g': keys, 'v': sm}
    print(f'\n{sport}: {used[sport]} aktivnosti, {sum(len(x) for x in bb.values())} uzoraka')
    for k, vv in zip(keys, sm):
        cnt = len(bb[k])
        print(f'  {k:+3d}%  {vv:5.1f} km/h   (n={cnt})')

out = 'const PACE=' + json.dumps(PACE) + ';\n'
dst = r"pacedata.js"
open(dst, 'w').write(out)
print('\nupisano:', dst)
