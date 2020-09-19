create table objects
(
    hash uuid  not null primary key,
    data jsonb not null,
    id   uuid generated always as (coalesce(data ->> '_id', data ->> 'id')::uuid) stored
);
create index if not exists objects_hash_idx on objects (hash);

create table stream_updates
(
    timestamp timestamptz not null,
    hash      uuid        not null,

    primary key (timestamp, hash)
);
create index if not exists stream_updates_hash_idx on stream_updates (hash);
create index if not exists stream_updates_timestamp_idx on stream_updates (timestamp);

create table game_updates
(
    timestamp  timestamptz not null,
    game_id    uuid        not null,
    hash       uuid        not null,
    search_tsv tsvector default null,

    primary key (timestamp, hash)
);
create index if not exists game_updates_hash_idx on game_updates (hash);
create index if not exists game_updates_timestamp_idx on game_updates (timestamp);
create index if not exists game_updates_search_tsv_idx on game_updates using gin (search_tsv);

create table site_updates
(
    timestamp timestamptz not null,
    path      text        not null,
    hash      uuid        not null,
    data      bytea       not null,

    unique (timestamp, path, hash)
);

create table team_updates
(
    timestamp timestamptz not null,
    team_id   uuid        not null,
    hash      uuid        not null,

    unique (timestamp, hash)
);

create extension pgcrypto;
create table player_updates
(
    timestamp timestamptz not null,
    player_id uuid        not null,
    hash      uuid        not null,

    unique (timestamp, hash)
);
create index player_updates_timestamp_idx on player_updates (timestamp);
create index player_updates_player_id_idx on player_updates (player_id);
create index player_updates_player_id_timestamp_idx on player_updates (player_id, timestamp desc);

create table misc_updates
(
    timestamp timestamptz not null,
    type      text        not null,
    hash      uuid        not null,

    unique (timestamp, type, hash)
);
create index misc_updates_timestamp_idx on misc_updates (timestamp);
create index misc_updates_type_idx on misc_updates (type);

create table players
(
    player_id uuid not null primary key
);