using System;
using System.Collections.Generic;
using System.Linq;

namespace CheatOnYourDayOnes.Casino
{
    // Pure rules: no wallet, scene, input or networking side effects.
    public static class CasinoRules
    {
        public static readonly int[] Wheel = {0,32,15,19,4,21,2,25,17,34,6,27,13,36,11,30,8,23,10,5,24,16,33,1,20,14,31,9,22,18,29,7,28,12,35,3,26};
        static readonly int[] RedNumbers={1,3,5,7,9,12,14,16,18,19,21,23,25,27,30,32,34,36};
        public static bool IsRed(int n) => Array.IndexOf(RedNumbers,n)>=0;
        public sealed class Bet
        {
            public readonly string Name;
            public readonly int[] Numbers;
            public readonly int ReturnMultiplier;
            public Bet(string name, params int[] numbers) { Name=name; Numbers=numbers; ReturnMultiplier=36/numbers.Length; }
        }
        public static List<Bet> Bets()
        {
            var list=new List<Bet>();
            for(int i=0;i<=36;i++) list.Add(new Bet(i.ToString(),i));
            list.Add(new Bet("Rot",Enumerable.Range(1,36).Where(IsRed).ToArray()));
            list.Add(new Bet("Schwarz",Enumerable.Range(1,36).Where(n=>!IsRed(n)).ToArray()));
            list.Add(new Bet("Gerade",Enumerable.Range(1,36).Where(n=>n%2==0).ToArray()));
            list.Add(new Bet("Ungerade",Enumerable.Range(1,36).Where(n=>n%2==1).ToArray()));
            list.Add(new Bet("1–18",Enumerable.Range(1,18).ToArray()));
            list.Add(new Bet("19–36",Enumerable.Range(19,18).ToArray()));
            for(int i=0;i<3;i++)
            {
                list.Add(new Bet("Dutzend "+(i+1),Enumerable.Range(1+12*i,12).ToArray()));
                list.Add(new Bet("Kolonne "+(i+1),Enumerable.Range(0,12).Select(n=>n*3+i+1).ToArray()));
            }
            list.Add(new Bet("Split 0/1",0,1));list.Add(new Bet("Split 0/2",0,2));list.Add(new Bet("Split 0/3",0,3));
            list.Add(new Bet("Trio 0/1/2",0,1,2));list.Add(new Bet("Trio 0/2/3",0,2,3));list.Add(new Bet("First Four",0,1,2,3));
            for(int n=1;n<=36;n++)
            {
                if(n%3!=0)list.Add(new Bet($"Split {n}/{n+1}",n,n+1));
                if(n<=33)list.Add(new Bet($"Split {n}/{n+3}",n,n+3));
                if(n<=32&&n%3!=0)list.Add(new Bet($"Carré {n}/{n+1}/{n+3}/{n+4}",n,n+1,n+3,n+4));
                if(n%3==1){list.Add(new Bet($"Straße {n}–{n+2}",n,n+1,n+2));if(n<=31)list.Add(new Bet($"Sechs {n}–{n+5}",Enumerable.Range(n,6).ToArray()));}
            }
            return list;
        }
        public static int RouletteReturn(Bet bet,int result,int stake) => stake<=0||result<0||result>36?0:Array.IndexOf(bet.Numbers,result)>=0?checked(stake*bet.ReturnMultiplier):0;
        public static List<int> Deck(Random rng)
        {
            var deck=Enumerable.Range(0,52).ToList();
            for(int i=51;i>0;i--){int j=rng.Next(i+1);int t=deck[i];deck[i]=deck[j];deck[j]=t;}
            return deck;
        }
        public static int Draw(List<int> deck){int c=deck[deck.Count-1];deck.RemoveAt(deck.Count-1);return c;}
        public static int Rank(int card)=>card%13+2;
        public static string Card(int card)=>new[]{"2","3","4","5","6","7","8","9","10","J","Q","K","A"}[card%13]+new[]{"♠","♥","♦","♣"}[card/13];
        public static int BlackjackTotal(IReadOnlyList<int> cards)
        {
            int sum=0,aces=0;foreach(int c in cards){int r=Rank(c);sum+=r==14?11:Math.Min(10,r);if(r==14)aces++;}
            while(sum>21&&aces-->0)sum-=10;return sum;
        }
        public static int BlackjackReturn(IReadOnlyList<int> hand,IReadOnlyList<int> dealer,int stake)
        {
            int p=BlackjackTotal(hand),d=BlackjackTotal(dealer);
            bool pn=p==21&&hand.Count==2,dn=d==21&&dealer.Count==2;
            if(p>21)return 0;if(dn)return pn?stake:0;if(pn)return stake*5/2;
            return d>21||p>d?stake*2:p==d?stake:0;
        }
        // Five-card draw versus dealer. Encoded rank includes every tie breaker.
        public static long PokerRank(IReadOnlyList<int> cards)
        {
            if(cards.Count!=5)throw new ArgumentException("Exactly five cards required");
            var ranks=cards.Select(Rank).OrderByDescending(x=>x).ToArray();
            var groups=ranks.GroupBy(x=>x).OrderByDescending(g=>g.Count()).ThenByDescending(g=>g.Key).ToArray();
            bool flush=cards.All(c=>c/13==cards[0]/13);
            int straight=ranks.Distinct().Count()==5&&ranks[0]-ranks[4]==4?ranks[0]:ranks.SequenceEqual(new[]{14,5,4,3,2})?5:0;
            int cat=flush&&straight>0?8:groups[0].Count()==4?7:groups[0].Count()==3&&groups[1].Count()==2?6:flush?5:straight>0?4:groups[0].Count()==3?3:groups[0].Count()==2&&groups[1].Count()==2?2:groups[0].Count()==2?1:0;
            var tie=(cat==8||cat==4)?new[]{straight}:groups.SelectMany(g=>Enumerable.Repeat(g.Key,g.Count())).ToArray();
            long value=cat;for(int i=0;i<5;i++)value=value*15+(i<tie.Length?tie[i]:0);return value;
        }
        public static string PokerName(long value){for(int i=0;i<5;i++)value/=15;return new[]{"High Card","Paar","Zwei Paare","Drilling","Straße","Flush","Full House","Vierling","Straight Flush"}[(int)value];}
        // Conservative arcade payouts: triple 7 = 20x, other triples = 5x,
        // and a pair returns 1x. The returned amount includes the stake.
        public static int SlotsReturn(int a,int b,int c,int stake)=>a==b&&b==c?stake*(a==0?20:5):a==b||b==c||a==c?stake:0;
    }
}
