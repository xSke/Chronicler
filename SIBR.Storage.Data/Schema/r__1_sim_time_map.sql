drop view if exists time_map_view;
drop materialized view if exists simdata_versions;

create materialized view simdata_versions as
    select
        type,
        update_id,
        first_seen,
        last_seen,
        hash,
        data
    from versions_view
    where type = 9 and first_seen is not null;
create unique index simdata_versions_pkey on simdata_versions (first_seen);

create view time_map_view as
    select
        season, tournament, day, type,
        min(first_seen) as start_time,
        lead(min(first_seen)) over (order by min(first_seen)) as end_time,
        
        min(first_seen) as first_seen,
        max(last_seen) as last_seen
    from (
        select 
            (data->>'season')::int as season,
            coalesce((data->>'tournament')::int, -1) as tournament,
            (data->>'day')::int as day,
            (data->>'phase')::int as phase,
            coalesce((array[
                -- See: https://docs.sibr.dev/docs/apis/docs/phases.md 
                'post_election',  -- 0
                'preseason',      -- 1
                'season',         -- 2
                'season',         -- 3
                'postseason',     -- 4
                'pre_election',   -- 5
                'pre_election',   -- 6
                'season',         -- 7
                null,             -- 8 (unused?)
                'bossfight',      -- 9
                'postseason',     -- 10
                'postseason',     -- 11
                'tournament',     -- 12
                'tournament',     -- 13
                'tournament',     -- 14
                'tournament'      -- 15
            ])[(data->>'phase')::int + 1], null) as type,
            first_seen, last_seen
        from simdata_versions
    ) as s2
    group by (season, tournament, day, type);