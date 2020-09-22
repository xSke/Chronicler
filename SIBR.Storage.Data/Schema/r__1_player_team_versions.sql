drop materialized view if exists player_versions;
create materialized view player_versions as
select entity_id as player_id, first_seen, last_seen, hash, data
from versions_view
where type = 1; -- Player = 1
create unique index player_versions_pkey on player_versions (player_id, first_seen);

drop materialized view if exists team_versions;
create materialized view team_versions as
select entity_id as team_id, first_seen, last_seen, hash, data
from versions_view
where type = 2; -- Team = 2
create unique index team_versions_pkey on team_versions (team_id, first_seen);