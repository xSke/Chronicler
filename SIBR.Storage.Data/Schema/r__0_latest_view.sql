create or replace view latest_view as
    select
        e.type,
        e.entity_id,
        latest.update_id,
        latest.timestamp,
        latest.hash,
        objects.data 
    from entities e
    inner join lateral (select u.update_id, u.hash, u.timestamp from updates u where u.type = e.type and u.entity_id = e.entity_id order by timestamp desc limit 1) as latest on true
    inner join objects using (hash);