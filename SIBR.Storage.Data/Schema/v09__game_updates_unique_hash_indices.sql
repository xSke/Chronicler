drop index if exists game_updates_unique_game_id_idx;
drop index if exists game_updates_unique_season_day_idx;
drop index if exists game_updates_unique_season_idx;
drop index if exists game_updates_unique_timestamp_idx;

create index game_updates_unique_game_id_idx on game_updates_unique (game_id, timestamp, hash);
create index game_updates_unique_season_day_idx on game_updates_unique (season, day, timestamp, hash);
create index game_updates_unique_season_idx on game_updates_unique (season, timestamp, hash);
create index game_updates_unique_timestamp_idx on game_updates_unique (timestamp, hash);