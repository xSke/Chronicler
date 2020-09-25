import json
from datetime import datetime

import requests

print("Fetching tributes")
tributes = requests.get("https://www.blaseball.com/api/getTribute").json()
player_ids = [tribute["playerId"] for tribute in tributes]

print("Fetching players")
players = requests.get("https://www.blaseball.com/database/players?ids=" + ",".join(player_ids)).json()
timestamp = datetime.utcnow()

updates = []
for player in players:
    updates.append({
        "type": 1,
        "timestamp": timestamp.isoformat() + "Z",
        "data": player
    })

print("Inserting player updates")
source_id = "c57920eb-dcca-438b-bdc6-b0ca3deb0368"
url = "http://localhost:4011/internal/updates?source={}".format(source_id)
resp = requests.post(url, data=json.dumps(updates), headers={"Content-Type": "application/json"})