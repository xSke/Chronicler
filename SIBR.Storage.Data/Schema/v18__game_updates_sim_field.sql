alter table games add column sim text not null default 'thisidisstaticyo';
alter table game_updates_unique add column sim text not null default 'thisidisstaticyo';

create index games_sim_season_day_idx on games(sim, season, day, game_id);
create index game_updates_unique_sim_season_day_idx on game_updates_unique (sim, season, day, timestamp, hash);

create or replace function game_updates_unique_insert_game_trg_func()
    returns trigger language plpgsql
as $$
begin
    insert into games (game_id, sim, season, tournament, day)
    values (new.game_id, new.sim, new.season, new.tournament, new.day)
    on conflict do nothing;

    return new;
end;
$$;
