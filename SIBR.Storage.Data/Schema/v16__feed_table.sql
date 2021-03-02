create table feed (
    id uuid primary key,
    timestamp timestamptz not null,
    data jsonb not null
);

create index feed_timestamp_idx on feed(timestamp);
create index feed_data_gin_idx on feed using gin(data);