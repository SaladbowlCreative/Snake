﻿using System.Collections;
using EntityNetwork;
using UnityEngine;
using UnityEngine.Networking;

public class EntityNetworkClient : NetworkBehaviour
{
    public static int LocalClientId;
    public static ClientZone LocalClientZone;

    private int _clientId;
    private ClientZone _zone;
    private ProtobufChannelToClientZoneInbound _zoneChannel;

    public int ClientId
    {
        get { return _clientId; }
    }

    // Registration

    public override void OnStartClient()
    {
        // In OnStartClient, ClientRpc is not called.
        // To workaround this limitation, use coroutine
        StartCoroutine(AddClientToZone());
    }

    private void OnDestroy()
    {
        EntityNetworkManager.Instance.RemoveClientToZone(_clientId);
    }

    private IEnumerator AddClientToZone()
    {
        // By OnStartClient's note
        yield return null;

        if (hasAuthority == false)
            yield break;

        var channel = new ProtobufChannelToServerZoneOutbound
        {
            OutboundChannel = new EntityNetworkChannelToServerZone { NetworkClient = this },
            TypeTable = EntityNetworkManager.Instance.GetTypeAliasTable(),
            TypeModel = EntityNetworkManager.Instance.GetTypeModel()
        };

        _clientId = (int)netId.Value;
        _zone = new ClientZone(EntityNetworkManager.Instance.GetClientEntityFactory(), channel);
        _zoneChannel = new ProtobufChannelToClientZoneInbound
        {
            TypeTable = EntityNetworkManager.Instance.GetTypeAliasTable(),
            TypeModel = EntityNetworkManager.Instance.GetTypeModel(),
            InboundClientZone = _zone
        };

        LocalClientId = _clientId;
        LocalClientZone = _zone;

        CmdAddClientToZone();
    }

    [Command]
    public void CmdAddClientToZone()
    {
        _clientId = (int)netId.Value;

        var result = EntityNetworkManager.Instance.AddClientToZone(_clientId, this);
        RpcAddClientToZoneDone(result);
    }

    [ClientRpc]
    public void RpcAddClientToZoneDone(bool added)
    {
        if (hasAuthority == false)
            return;

        Debug.LogFormat("EntityNetworkClient({0}).RpcAddClientToZoneDone({1})", _clientId, added);
        if (added)
        {
            CmdAddClientToZoneDone();
        }
        else
        {
            _clientId = 0;
            _zone = null;
            _zoneChannel = null;
            LocalClientId = 0;
        }
    }

    [Command]
    public void CmdAddClientToZoneDone()
    {
        Debug.LogFormat("EntityNetworkClient({0}).CmdAddClientToZoneDone", _clientId);
    }

    // Zone Channel

    [Command]
    public void CmdBuffer(byte[] bytes)
    {
        EntityNetworkManager.Instance.WriteZoneChannel(ClientId, bytes);
    }

    [ClientRpc]
    public void RpcBuffer(byte[] bytes)
    {
        if (hasAuthority == false)
            return;

        _zoneChannel.Write(bytes);
    }

    private void Update()
    {
        if (_zone != null)
            ((EntityTimerProvider)_zone.TimerProvider).ProcessWork();
    }

    public override void OnNetworkDestroy()
    {
        Debug.LogFormat("EntityNetworkClient({0}).OnNetworkDestroy", _clientId);
    }
}
