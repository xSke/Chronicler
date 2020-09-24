do
$$
    begin
        drop materialized view if exists game_updates_unique;
    exception
        when others then
    end;
$$;

do
$$
    begin
        drop view if exists game_updates_unique;
    exception
        when others then
    end;
$$;

create table game_updates_unique
(
    hash       uuid        not null primary key,
    game_id    uuid        not null,
    timestamp  timestamptz not null,
    data       jsonb       not null,
    season     smallint    not null,
    day        smallint    not null,
    search_tsv tsvector
);
create index game_updates_unique_search_tsv_idx on game_updates_unique using gist (search_tsv);
create index game_updates_unique_data_idx on game_updates_unique using gin (data jsonb_path_ops);
create index game_updates_unique_game_id_idx on game_updates_unique (game_id, timestamp);
create index game_updates_unique_season_day_idx on game_updates_unique (season, day, timestamp);
create index game_updates_unique_season_idx on game_updates_unique (season, timestamp);
create index game_updates_unique_timestamp_idx on game_updates_unique (timestamp);

insert into game_updates_unique(hash, game_id, timestamp, data, season, day, search_tsv)
select hashes.hash, game_id, timestamp, (select data from objects where objects.hash = hashes.hash), season, day, null
from (select distinct hash from game_updates) as hashes
         inner join lateral (select *
                             from game_updates
                             where game_updates.hash = hashes.hash
                             order by timestamp
                             limit 1) as game_update on true
on conflict (hash) do update set timestamp = least(game_updates_unique.timestamp, excluded.timestamp);

create index game_updates_unique_data_idx2 on game_updates_unique using gin (data jsonb_path_ops, timestamp);
