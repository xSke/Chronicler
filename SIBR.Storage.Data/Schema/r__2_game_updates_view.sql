select distinct on (game_id, hash) (data ->> 'lastUpdate'), *
from game_updates
         inner join objects using (hash)
where season = 5
  and day = 3
order by game_id, hash, timestamp;

create view game_updates_unique as

    
analyze game_updates;
    
explain analyze
    

-- explain analyze
    
    
with all_updates as (
    select *, exists(select 1
                     from game_updates g2
                     where g2.timestamp < game_updates.timestamp
                       and g2.hash = game_updates.hash) as dupe from game_updates
)

;


select * from game_updates 
order by timestamp;


create view game_updates_unique as
select *,
       (select data from objects where objects.hash = game_updates.hash)
from game_updates
where not exists (select 1
              from game_updates g2
              where g2.timestamp < game_updates.timestamp
                and g2.hash = game_updates.hash);

select *
from (
         select exists(select 1
                       from game_updates g2
                       where g2.timestamp < game_updates.timestamp
                         and g2.hash = game_updates.hash) as dupe,
                *,
                (select data from objects where objects.hash = game_updates.hash)
         from game_updates
         order by timestamp
     ) as updates
where (not dupe) and season = 6 and day = 10
limit 100

select 1