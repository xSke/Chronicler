drop view if exists site_updates_unique;
create view site_updates_unique as
    select *,
        octet_length(data) as size
    from site_updates
    inner join binary_objects bo using (hash)
    where not exists(
        select 1 from site_updates s2
        where 
            s2.timestamp < site_updates.timestamp and
            s2.hash = site_updates.hash and
            s2.path = site_updates.path
    );