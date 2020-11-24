create extension if not exists pgcrypto;

create table objects
(
    hash uuid  not null primary key,
    data jsonb not null
);

create table updates
(
    source_id uuid        not null,
    type      smallint    not null,
    timestamp timestamptz not null,
    hash      uuid        not null,
    entity_id uuid,
    primary key (timestamp, hash)
);
create index updates_type_entity_timestamp_hash_idx on updates (type, entity_id, timestamp, hash);

create table site_updates
(
    source_id uuid        not null,
    timestamp timestamptz not null,
    path      text        not null,
    hash      uuid        not null,
    data      bytea       not null,

    unique (timestamp, path, hash)
);

create table game_updates
(
    source_id uuid        not null,
    timestamp timestamptz not null,
    game_id   uuid        not null,
    hash      uuid        not null,
    season    smallint    not null,
    day       smallint    not null,
    
    primary key (game_id, timestamp)
);
create index game_updates_season_day_idx on game_updates (season, day, timestamp);
create index game_updates_season_idx on game_updates (season, timestamp);
create index game_updates_game_id_idx on game_updates (game_id, timestamp);
create index game_updates_game_id_hash_idx on game_updates (game_id, hash, timestamp);
create index game_updates_timestamp_idx on game_updates (timestamp);
