import json
from datetime import datetime

import requests

source_id = "a4715d03-d092-4ef4-a3cc-4a19776a6fd5"
for (season, num_games) in [(0, 115), (1, 99)]:
    updates = []
    for day in range(num_games):
        url = "https://www.blaseball.com/database/games?season={}&day={}".format(season, day)
        print("Fetching {}".format(url))
        games = requests.get(url).json()
        timestamp = datetime.utcnow().isoformat() + "Z"
        for i, game in enumerate(games):
            updates.append({"type": 4, "timestamp": timestamp, "data": game})

    if updates:
        body = json.dumps(updates)
        url = "http://localhost:5003/internal/gameupdates?source={}".format(source_id)
        resp = requests.post(url, data=body, headers={"Content-Type": "application/json"})
        print(resp.text)
