world_asset_config 'gta5/viiv' {
    -- workaround to free up modelinfos
    ignore_lod_modelinfos = true,

    -- global world asset file to replace gta.dat
    world_definition = 'gta5.dat',

    -- weird hacks
    modelinfo_deadlock_hack = true,
    bounds_arent_cdimage = true,
    entity_sanity = true,
    static_bound_sanity = true,
    odd_wait_deadlock = true,
	definitely_more_navigable = true,
    bigger_paths = true,

    -- limits
    -- these can't be done at runtime yet, so they're static for now
    --[[limit_static_bounds = 7500,
    limit_transform_matrix = 13000 * 6,
    limit_building = 32000 * 8, -- needs more streaming ipls later
    limit_drawbldict = 3000,
    limit_ptrnode_single = 100000]]
}

files {
    'gta5.dat'
}
