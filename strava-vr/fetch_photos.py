"""Povuce URL-ove slika sa aktivnosti -> viewer/data/photos.json {activity_id: [url,...]}"""
import json, os, time, urllib.error
from strava_fetch import load_env, get_token, http_json, BASE, HERE

routes = json.load(open(os.path.join(HERE, 'viewer/data/routes.geojson'), encoding='utf-8'))
ids = [f['properties']['id'] for f in routes['features']]
cid, sec = load_env()
token = get_token(cid, sec)

out = os.path.join(HERE, 'viewer/data/photos.json')
photos = {}
done = set()
if os.path.exists(out):
    photos = json.load(open(out, encoding='utf-8'))
    done = set(photos.keys())
if os.path.exists(out + '.done'):
    done |= set(json.load(open(out + '.done', encoding='utf-8')))
for n, aid in enumerate(ids):
    if str(aid) in done:
        continue
    try:
        items = http_json(f"{BASE}/activities/{aid}/photos?size=1000&photo_sources=true", token=token)
    except urllib.error.HTTPError as e:
        if e.code == 429:
            print('rate limit, stajem'); break
        continue
    urls = []
    for it in items or []:
        u = (it.get('urls') or {})
        best = u.get('1000') or u.get('600') or (list(u.values())[0] if u else None)
        if best: urls.append(best)
    done.add(str(aid))
    if urls:
        photos[str(aid)] = urls
        print(f'  {aid}: {len(urls)} slika')
    time.sleep(0.25)

json.dump(photos, open(out, 'w', encoding='utf-8'))
json.dump(sorted(done), open(out + '.done', 'w', encoding='utf-8'))
print(f'ukupno {sum(len(v) for v in photos.values())} slika sa {len(photos)} aktivnosti -> {out}')
