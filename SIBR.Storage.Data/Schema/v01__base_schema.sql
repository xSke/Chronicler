create table stream_updates
(
    timestamp      timestamptz not null,
    last_timestamp timestamptz not null,
    hash           uuid        not null,
    data           jsonb,

    unique (hash)
);
create index if not exists stream_updates_hash_idx on stream_updates (hash);
create index if not exists stream_updates_timestamp_idx on stream_updates (timestamp);

create table site_updates
(
    timestamp      timestamptz not null,
    last_timestamp timestamptz not null,
    path           text        not null,
    hash           uuid        not null,
    data           bytea       not null,

    unique (path, hash)
);

create table game_updates
(
    timestamp  timestamptz not null,
    hash       uuid        not null,
    data       jsonb       not null,
    search_tsv tsvector generated always as (to_tsvector('english', data ->> 'lastUpdate')) stored,

    unique (hash)
);
create index if not exists game_updates_hash_idx on game_updates (hash);
create index if not exists game_updates_timestamp_idx on game_updates (timestamp);
create index if not exists game_updates_lastupdate_search_idx on game_updates using gin (search_tsv);

create table team_updates
(
    timestamp      timestamptz not null,
    last_timestamp timestamptz not null,
    team_id        uuid        not null,
    hash           uuid        not null,
    data           jsonb       not null,

    unique (hash)
);

create table player_updates
(
    timestamp      timestamptz not null,
    last_timestamp timestamptz not null,
    player_id      uuid        not null,
    hash           uuid        not null,
    data           jsonb       not null,

    unique (hash)
);