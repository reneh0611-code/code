using System;
using System.Collections;
using System.Collections.Generic;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEngine;

namespace CheatOnYourDayOnes.Casino
{
    [Serializable]
    public sealed class CasinoSnapshot
    {
        public int station=-1,round,stake,payout,winner=-1;
        public long cash;
        public string phase="ready",message="Einsatz wählen.",game;
        public int[] hand=Array.Empty<int>(),dealer=Array.Empty<int>(),reels=Array.Empty<int>();
    }
    // One owner-authorized game stream per player, using the existing server-only wallet.
    public sealed class CasinoPlayerGames : NetworkBehaviour
    {
        public CasinoSnapshot State {get;private set;}=new CasinoSnapshot();
        public event Action<CasinoSnapshot> Changed;
        readonly System.Random random=new System.Random();
        readonly List<CasinoRules.Bet> rules=CasinoRules.Bets();
        readonly List<int> hand=new List<int>(),dealer=new List<int>();
        List<int> deck;
        PlayerAgent agent;
        CasinoSession salon;
        CasinoSnapshot server=new CasinoSnapshot();
        int lockedPayout;
        int[] betIds,betStakes;
        float lastRequest=-100;
        public void Request(int table,string action,int stake=0,int[] ids=null,int[] stakes=null)
        {if(IsOwner&&IsSpawned)ActionRpc(table,action,stake,ids??Array.Empty<int>(),stakes??Array.Empty<int>());}

        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        void ActionRpc(int table,string action,int stake,int[] ids,int[] stakes)
        {
            if(!IsServer||!IsSpawned)return;
            if(agent==null)agent=GetComponent<PlayerAgent>();
            if(salon==null)salon=FindAnyObjectByType<CasinoSession>();
            if(agent==null||salon==null||table<0||table>=salon.stations.Length)return;
            var station=salon.stations[table];
            bool isCurrent=server.station==table;
            if(action=="leave")
            {
                if(!isCurrent)return;
                if(server.phase=="cards")Stand();
                // A running wheel remains a committed round and settles even after the UI closes.
                return;
            }
            if(Time.unscaledTime-lastRequest<.15f)return;lastRequest=Time.unscaledTime;
            Vector3 delta=agent.transform.position-station.transform.position;
            if(delta.sqrMagnitude>6.5f*6.5f||Mathf.Abs(delta.y)>2)return;
            if(server.phase=="spin"){if(isCurrent&&action=="open")Send();return;}
            if(server.phase=="cards")
            {
                if(!isCurrent)return;
                if(action=="hit"){hand.Add(CasinoRules.Draw(deck));if(CasinoRules.BlackjackTotal(hand)>=21)Stand();else Send();}
                else if(action=="stand")Stand();
                else if(action=="double"&&hand.Count==2&&agent.Wallet.TrySpendCashServer(server.stake,"Casino blackjack double"))
                {server.stake*=2;hand.Add(CasinoRules.Draw(deck));Stand();}
                return;
            }
            if(action=="open")
            {
                server.station=table;server.game=station.game.ToString();server.phase="ready";server.message="Einsatz wählen.";server.stake=0;server.payout=0;
                hand.Clear();dealer.Clear();Send();return;
            }
            if(!isCurrent)return;
            if(action=="roulette"&&station.game==CasinoGame.Roulette)
            {
                if(ids==null||stakes==null||ids.Length!=stakes.Length||ids.Length==0||ids.Length>rules.Count)return;
                long total=0;var distinct=new HashSet<int>();
                for(int i=0;i<ids.Length;i++){if(ids[i]<0||ids[i]>=rules.Count||!distinct.Add(ids[i])||stakes[i]<10||stakes[i]>1000||stakes[i]%10!=0)return;total+=stakes[i];}
                if(total>10000||!Spend((int)total))return;
                betIds=(int[])ids.Clone();betStakes=(int[])stakes.Clone();server.winner=random.Next(37);
                lockedPayout=0;for(int i=0;i<ids.Length;i++)lockedPayout+=CasinoRules.RouletteReturn(rules[ids[i]],server.winner,stakes[i]);
                BeginSpin();
            }
            else if(action=="slots"&&station.game==CasinoGame.Slots)
            {
                if(!ValidStake(stake)||!Spend(stake))return;
                server.reels=new[]{random.Next(6),random.Next(6),random.Next(6)};
                lockedPayout=CasinoRules.SlotsReturn(server.reels[0],server.reels[1],server.reels[2],stake);BeginSpin();
            }
            else if(action=="deal"&&station.game==CasinoGame.Blackjack)
            {
                if(!ValidStake(stake)||!Spend(stake))return;
                server.round++;server.phase="cards";server.message="Karte nehmen, stehen oder verdoppeln.";
                deck=CasinoRules.Deck(random);hand.Clear();dealer.Clear();
                for(int i=0;i<2;i++){hand.Add(CasinoRules.Draw(deck));dealer.Add(CasinoRules.Draw(deck));}
                if(CasinoRules.BlackjackTotal(hand)==21||CasinoRules.BlackjackTotal(dealer)==21)Stand();else Send();
            }
        }
        static bool ValidStake(int stake)=>stake>=10&&stake<=1000&&stake%10==0;
        bool Spend(int amount)
        {
            if(!agent.Wallet.TrySpendCashServer(amount,"Casino bet")){server.message="Nicht genügend Bargeld.";Send();return false;}
            server.stake=amount;server.payout=0;return true;
        }
        void BeginSpin(){server.round++;server.phase="spin";server.message="Keine weiteren Einsätze.";Send();StartCoroutine(SettleSpin(server.round));}
        IEnumerator SettleSpin(int round)
        {
            yield return new WaitForSecondsRealtime(6.2f);
            if(!IsSpawned||server.round!=round||server.phase!="spin")yield break;
            Pay(lockedPayout,server.game=="Roulette"?$"Ergebnis: {server.winner} {(server.winner==0?"ZERO":CasinoRules.IsRed(server.winner)?"ROT":"SCHWARZ")}":"Walzen gestoppt.");
        }
        void Stand()
        {
            if(server.phase!="cards")return;
            if(CasinoRules.BlackjackTotal(hand)<=21)while(CasinoRules.BlackjackTotal(dealer)<17)dealer.Add(CasinoRules.Draw(deck));
            Pay(CasinoRules.BlackjackReturn(hand,dealer,server.stake),$"Du {CasinoRules.BlackjackTotal(hand)} · Dealer {CasinoRules.BlackjackTotal(dealer)}");
        }
        void Pay(int payout,string result)
        {
            // Transition before crediting to make settlement idempotent against further requests.
            server.phase="settled";server.payout=payout;
            if(payout>0)agent.Wallet.AddCashServer(payout,"Casino payout");
            server.message=result+$" · Rückzahlung {payout} € · Netto {payout-server.stake:+0;-0;0} €";Send();
        }
        void Send()
        {
            server.cash=agent.Wallet.Cash.Value;server.hand=hand.ToArray();
            server.dealer=server.phase=="cards"?new[]{dealer[0],-1}:dealer.ToArray();
            SnapshotRpc(JsonUtility.ToJson(server));
        }
        [Rpc(SendTo.Owner)]
        void SnapshotRpc(string json){State=JsonUtility.FromJson<CasinoSnapshot>(json);Changed?.Invoke(State);}
        public override void OnNetworkDespawn(){StopAllCoroutines();Changed=null;}
    }
}
