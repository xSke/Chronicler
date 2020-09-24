create index game_updates_unique_state_idx on
    game_updates_unique(game_id, timestamp, ((data->>'gameStart')::bool), ((data->>'gameComplete')::bool));