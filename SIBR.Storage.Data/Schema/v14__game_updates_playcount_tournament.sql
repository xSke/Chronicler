alter table game_updates add column tournament smallint default -1;
alter table game_updates_unique add column play_count smallint default null;
alter table game_updates_unique add column tournament smallint default -1;