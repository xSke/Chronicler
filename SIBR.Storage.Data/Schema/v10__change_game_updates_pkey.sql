alter table game_updates drop constraint game_updates_pkey;
alter table game_updates add primary key (game_id, timestamp, hash, source_id)
