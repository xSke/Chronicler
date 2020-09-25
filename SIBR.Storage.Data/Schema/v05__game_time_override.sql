-- We're missing data from S1/S2 but reasonable estimates can go here, picked up by games_view
-- Not filled in by this migration, because they're subject to manual change :)
create table game_time_overrides (
    game_id uuid primary key not null,
    start_time timestamptz not null
);