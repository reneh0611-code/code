using System;
using System.Linq;
using CheatOnYourDayOnes.Casino;

public static class VerifyCasinoRules
{
    public static string Run()
    {
        int checks=0;
        Action<bool,string> check=(ok,message)=>{checks++;if(!ok)throw new Exception(message);};
        check(CasinoRules.Wheel.Length==37&&CasinoRules.Wheel.Distinct().Count()==37,"Wheel must contain 37 unique pockets");
        check(CasinoRules.Wheel.OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,37)),"Single zero wheel range");
        var bets=CasinoRules.Bets();
        foreach(var bet in bets)
        {
            check(bet.Numbers.Distinct().Count()==bet.Numbers.Length,"Duplicate coverage: "+bet.Name);
            int totalReturn=0;
            for(int result=0;result<=36;result++)
            {
                int payout=CasinoRules.RouletteReturn(bet,result,10);
                check(payout==(bet.Numbers.Contains(result)?360/bet.Numbers.Length:0),"Wrong payout: "+bet.Name);
                totalReturn+=payout;
            }
            check(totalReturn==360,"Coverage and payout disagree: "+bet.Name);
        }
        foreach(var bet in bets.Skip(37).Take(12))check(CasinoRules.RouletteReturn(bet,0,10)==0,"Zero must lose outside bets");
        check(CasinoRules.BlackjackTotal(new[]{12,25,8})==12,"Multiple aces");
        check(CasinoRules.BlackjackReturn(new[]{12,8},new[]{7,8},20)==50,"Natural blackjack pays 3:2 plus stake");
        check(CasinoRules.BlackjackReturn(new[]{12,8},new[]{25,21},20)==20,"Two naturals push");
        check(CasinoRules.BlackjackReturn(new[]{8,21,34},new[]{8,7},20)==0,"Player bust loses");
        check(CasinoRules.BlackjackReturn(new[]{8,7},new[]{8,21,34},20)==40,"Dealer bust pays even money");
        check(CasinoRules.BlackjackReturn(new[]{8,7},new[]{21,20},20)==20,"Equal totals push");
        var deck=CasinoRules.Deck(new Random(10));
        check(deck.Count==52&&deck.Distinct().Count()==52,"Deck must contain 52 unique cards");
        for(int a=0;a<6;a++)for(int b=0;b<6;b++)for(int c=0;c<6;c++)
        {
            int expected=a==b&&b==c?(a==0?200:50):(a==b||b==c||a==c?10:0);
            check(CasinoRules.SlotsReturn(a,b,c,10)==expected,"Slot payout");
        }
        return checks+" casino rule checks passed; "+bets.Count+" roulette bets, 37 single-zero outcomes, blackjack edge cases, 216 slot outcomes.";
    }
}
