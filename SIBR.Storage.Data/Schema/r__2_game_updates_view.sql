create view game_updates_unique as
select *,
       (select data from objects where objects.hash = game_updates.hash)
from game_updates
where not exists (select 1
              from game_updates g2
              where g2.timestamp < game_updates.timestamp
                and g2.hash = game_updates.hash);