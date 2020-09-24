drop materialized view if exists idols_versions;
create materialized view idols_versions as
    select first_seen, last_seen, data from versions_view where type = 5;
create unique index idols_versions_pkey on idols_versions (first_seen);

drop materialized view if exists tributes_versions;
create materialized view tributes_versions as
    select distinct on (first_seen)
        first_seen, last_seen, data from versions_view where type = 6;
create unique index tributes_versions_pkey on tributes_versions (first_seen);