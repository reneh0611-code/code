#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using CheatOnYourDayOnes.Casino;
using CheatOnYourDayOnes.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CasinoPlayValidation
{
    static CasinoPlayValidation(){EditorApplication.update+=Tick;}
    [MenuItem("Day Ones/Casino/Start Full Casino Test")]
    public static void Arm(){SessionState.SetBool("CasinoValidationArmed",true);EditorApplication.isPlaying=true;}
    static void Tick()
    {
        if(!SessionState.GetBool("CasinoValidationArmed",false)||!Application.isPlaying)return;
        var p=NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerAgent>();
        if(p==null||!p.IsSpawned||Cursor.lockState!=CursorLockMode.Locked)return;
        SessionState.SetBool("CasinoValidationArmed",false);Validate();
    }
    [MenuItem("Day Ones/Casino/Play - Go To Roulette")]
    public static void Visit()
    {
        var s=UnityEngine.Object.FindAnyObjectByType<CasinoSession>();var p=NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerAgent>();
        if(!Application.isPlaying||s==null||p==null){Debug.LogError("Start Play Mode and enter the game first.");return;}
        s.Close();p.GetComponent<NetworkPlayerController>().TeleportServerAuthoritative(s.stations[0].transform.TransformPoint(new Vector3(0,0,-3)),s.transform.rotation);
        Debug.Log("At roulette. Press E to play.");
    }
    [MenuItem("Day Ones/Casino/Play - Validate All Games")]
    public static void Validate()
    {
        var s=UnityEngine.Object.FindAnyObjectByType<CasinoSession>();var p=NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerAgent>();
        if(!Application.isPlaying||s==null||p==null||!p.IsServer){Debug.LogError("Start a local host and enter the game first.");return;}
        s.Close();s.StartCoroutine(Run(s,p));
    }
    static void Check(bool result,string name){if(!result)throw new Exception("CASINO PLAY TEST FAILED: "+name);Debug.Log("CASINO PASS: "+name);}
    static IEnumerator Run(CasinoSession s,PlayerAgent p)
    {
        var g=p.GetComponent<CasinoPlayerGames>();var move=p.GetComponent<NetworkPlayerController>();
        var oldPosition=p.transform.position;var oldRotation=p.transform.rotation;long oldCash=p.Wallet.Cash.Value;
        bool oldLock=move.IsCombatMovementLocked;move.SetCombatMovementLocked(true);
        try
        {
            for(int table=0;table<s.stations.Length;table++)
            {
                move.TeleportServerAuthoritative(s.stations[table].transform.TransformPoint(new Vector3(0,0,-2.8f)),s.transform.rotation);
                yield return new WaitForSecondsRealtime(.5f);g.Request(table,"open");yield return new WaitForSecondsRealtime(.4f);
                Check(g.State.station==table&&g.State.phase=="ready","Open station "+table);
                for(int round=0;round<2;round++)
                {
                    long before=p.Wallet.Cash.Value;
                    if(s.stations[table].game==CasinoGame.Roulette)
                    {
                        var ids=Enumerable.Range(0,37).ToArray();var stakes=Enumerable.Repeat(10,37).ToArray();
                        g.Request(table,"roulette",0,ids,stakes);g.Request(table,"roulette",0,ids,stakes);
                        yield return new WaitForSecondsRealtime(.4f);Check(p.Wallet.Cash.Value==before-370,"Roulette debit once despite duplicate request");
                        yield return new WaitForSecondsRealtime(6.3f);
                        Check(g.State.phase=="settled"&&g.State.winner>=0&&g.State.winner<=36,"Roulette finishes on one of 37 numbers");
                        Check(p.Wallet.Cash.Value==before-10&&g.State.payout==360,"Roulette exact 35:1 settlement round "+round);
                    }
                    else if(s.stations[table].game==CasinoGame.Slots)
                    {
                        g.Request(table,"slots",10);yield return new WaitForSecondsRealtime(6.7f);
                        Check(g.State.phase=="settled"&&g.State.reels.Length==3,"Slot settles station "+table);
                        var r=g.State.reels;int payout=CasinoRules.SlotsReturn(r[0],r[1],r[2],10);
                        Check(p.Wallet.Cash.Value==before-10+payout,"Slot wallet matches reels");
                    }
                    else
                    {
                        g.Request(table,"deal",10);yield return new WaitForSecondsRealtime(.4f);
                        if(g.State.phase=="cards"){Check(g.State.dealer[1]==-1,"Dealer hole card hidden");g.Request(table,round==0?"stand":"double");yield return new WaitForSecondsRealtime(.4f);}
                        Check(g.State.phase=="settled","Blackjack settles");
                        int payout=CasinoRules.BlackjackReturn(g.State.hand,g.State.dealer,g.State.stake);
                        Check(g.State.payout==payout&&p.Wallet.Cash.Value==before-g.State.stake+payout,"Blackjack payout and wallet agree");
                    }
                    yield return new WaitForSecondsRealtime(.4f);
                }
            }
            Debug.Log("CASINO PLAY VALIDATION COMPLETE: 2 roulette rounds, 2 blackjack rounds, 6 slot rounds. All passed.");
        }
        finally{p.Wallet.Cash.Value=oldCash;move.TeleportServerAuthoritative(oldPosition,oldRotation);move.SetCombatMovementLocked(oldLock);}
    }
}
#endif
