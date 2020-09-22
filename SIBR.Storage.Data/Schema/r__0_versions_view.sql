create or replace view versions_view as
with last_for_each as (
    select type, entity_id, max(timestamp) as timestamp from updates group by (type, entity_id)
)
select versions.entity_id,
       versions.type,
       versions.first_seen,
       coalesce(versions.last_seen, last_for_each.timestamp) as last_seen,
       versions.hash,
       versions.data
from (
         select type,
                entity_id,
                timestamp                                                                     as first_seen,
                lead(last_timestamp) over (partition by (type, entity_id) order by timestamp) as last_seen,
                hash,
                (select data from objects where objects.hash = updates.hash)                  as data
         from (
                  select timestamp,
                         hash,
                         entity_id,
                         type,
                         lag(hash) over (partition by (type, entity_id) order by timestamp)      as last_hash,
                         lag(timestamp) over (partition by (type, entity_id) order by timestamp) as last_timestamp
                  from updates
              ) as updates
         where (last_hash is null or last_hash != hash)
     ) as versions
         left join last_for_each on last_for_each.type = versions.type and last_for_each.entity_id = versions.entity_id;



