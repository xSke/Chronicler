drop view if exists site_updates_unique;
create view site_updates_unique as
    select *,
        (select data from binary_objects where binary_objects.hash = site_updates.hash)
    from site_updates
    where not exists(
        select 1 from site_updates s2
        where 
            s2.timestamp < site_updates.timestamp and
            s2.hash = site_updates.hash and
            s2.path = site_updates.path
    );