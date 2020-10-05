create or replace view observations_versioned_view as
    select
        update_id,
        type,
        entity_id, 
        timestamp,
        hash,
        version_increment = 1 as is_new_version,
        lag(timestamp) over w as prev_timestamp,
        count(version_increment) over w as version,
        version_increment
    from (
        select 
            update_id, type, timestamp, hash, entity_id,
            case
                when (lag(hash) over w) is distinct from hash then 1
            end as version_increment
        from updates
        window w as (partition by type, entity_id order by timestamp rows between unbounded preceding and current row)
    ) as observations_with_increment
    window w as (partition by type, entity_id order by timestamp rows between unbounded preceding and current row);

create or replace view versions_view as
    select
        entity_id,
        type,
        timestamp as first_seen,
        coalesce(
            lead(prev_timestamp) over w,
            (select max(timestamp) from updates u where u.hash = ovv.hash)
        ) as last_seen,
        ovv.hash,
        (select data from objects o where o.hash = ovv.hash) as data,
        version,
        update_id
    from observations_versioned_view ovv
    where ovv.version_increment = 1
    window w as (partition by type, entity_id order by timestamp rows between unbounded preceding and current row);