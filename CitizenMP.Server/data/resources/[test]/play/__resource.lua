resource_type 'gametype' { name = 'Freeroam' }

dependencies {
    "spawnmanager",
    "mapmanager"
}

client_script 'play_client.lua'
server_script 'play_server.lua'
