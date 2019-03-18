// custom NetworkManager that simply assigns the correct racket positions when
// spawning players. the built in RoundRobin spawn method wouldn't work after
// someone reconnects (both players would be on the same side).
using UnityEngine;
using Mirror;
using Mirror.LiteNetLib4Mirror;

public class LiteNetLib4MirrorNetworkManagerPong : LiteNetLib4MirrorNetworkManager
{
    public Transform leftRacketSpawn;
    public Transform rightRacketSpawn;
    public GameObject ballPrefab;
    private GameObject _ball;

    public override void OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)
    {
        // add player at correct spawn position
        Transform start = numPlayers == 0 ? leftRacketSpawn : rightRacketSpawn;
        GameObject player = Instantiate(playerPrefab, start.position, start.rotation);
        NetworkServer.AddPlayerForConnection(conn, player);

        // spawn ball if two players
        if (numPlayers == 2)
        {
            _ball = Instantiate(ballPrefab);
            NetworkServer.Spawn(_ball);
        }
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        // destroy ball
        if (_ball != null)
            NetworkServer.Destroy(_ball);

        // call base functionality (actually destroys the player)
        base.OnServerDisconnect(conn);
    }
}
