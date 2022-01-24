create table pusher_events (
    id uuid primary key default gen_random_uuid(),
    channel text not null,
    event text not null,
    timestamp timestamptz not null,
    raw text not null,
    data jsonb
);

create index pusher_events_timestamp_event_channel on pusher_events(timestamp, event, channel);