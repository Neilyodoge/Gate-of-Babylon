using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

public partial class FarmPanel
{
    private void CrystalPreview(string root,string name,int size=0)
    {
        ReadingPreview(root,"crystal-"+name,size,true);
        ReviewTarget(GuidePrimary(),"crystal "+name+" primary "+size);
    }
    private void CrystalFixture(int pet,int stage=0)
    {
        EverydayFixture();G.S.Xp=Enumerable.Repeat(300,FarmGame.PetCount).ToArray();G.S.Bond=6;G.S.SeedBond=6;G.S.SeedStage=stage;G.S.Crystals=0;
        G.S.BlessingOffers=new[]{0,1,3};G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(3).Ticks;
        OpenSmallEvolution(pet);smallGrowthStep=1;
    }
    internal void RenderCrystalJourneyChecks(string root)
    {
        Action<bool,string> check=FarmHeadlessTests.Check;
        for(int size=0;size<3;size++)foreach(int pet in new[]{0,4}){
            CrystalFixture(pet);CrystalPreview(root,"source-"+pet,size);
            string before=FarmPracticeTests.Xml(G.S);var source=GuidePrimary();GuideClick(source);CrystalPreview(root,"preparation-"+pet,size);
            check(HasCrystalPreparation&&crystalPreparationPet==pet&&G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"crystal preparation is read-only and remembers exact pet");
            ReviewTarget(UIAction("先回去看进化"),"crystal cancel "+size);ReviewTarget(LaterExpander("repeat-departure-details"),"crystal preparation details "+size);
            var staleStart=GuidePrimary();KeyPress(Key.Escape);CrystalPreview(root,"cancel-"+pet,size);
            check(Tab=="进化"&&smallGrowthPet==pet&&smallGrowthStep==1&&!HasCrystalPreparation&&FarmPracticeTests.Xml(G.S)==before,"Esc resumes original requirement without expense");
            staleStart.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(G.AtHome&&FarmPracticeTests.Xml(G.S)==before,"cancelled departure cannot start from stale event");
            GuideClick(GuidePrimary());CrystalPreview(root,"prepare-again-"+pet,size);
            OpenPreparationCards();CrystalPreview(root,"card-"+pet,size);KeyPress(Key.Escape);CrystalPreview(root,"card-return-"+pet,size);
            check(HasCrystalPreparation&&crystalPreparationPet==pet,"optional preparation cards retain target");
            GuideClick(GuidePrimary());check(!G.AtHome&&G.R.CrystalTargetPet==pet&&G.R.CrystalTargetStage==0&&!HasCrystalPreparation,"only committed departure puts target on this run");
            G.ToggleJourneyPause();string paused=FarmPracticeTests.Xml(G.R);owner.TickClock(DateTime.UtcNow.AddMinutes(2),false,true,false);check(FarmPracticeTests.Xml(G.R)==paused,"target journey remains paused under production clock");
            G.ToggleJourneyPause();owner.TickClock(DateTime.UtcNow.AddMinutes(2),false,true,false);G.AdvanceJourney(DateTime.UtcNow.AddHours(1));CrystalPreview(root,"receipt-"+pet,size);
            check(G.R.Outcome=="victory"&&G.R.RewardCrystals==2&&GuideWords().Contains("晶石")&&!GuideWords().Contains("给田施肥"),"crystal journey foregrounds actual collected crystals");
            ReviewTarget(UIAction("先回小院"),"crystal home "+size);ReviewTarget(UIChildren<Expander>((DependencyObject)Content).Single(e=>Convert.ToString(e.Header)=="还有哪些收获"),"crystal extra "+size);
            var receipt=GuidePrimary();int crystals=G.S.Crystals;var roster=(int[])G.S.Team.Clone();GuideClick(receipt);CrystalPreview(root,"returned-"+pet,size);
            check(G.AtHome&&Tab=="进化"&&smallGrowthStep==1&&smallGrowthPet==pet&&G.S.Crystals==crystals&&SmallEvolutionStage(pet)==0&&G.S.Team.SequenceEqual(roster),"receipt returns correct requirement without evolving or changing roster");
            before=FarmPracticeTests.Xml(G.S);receipt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(FarmPracticeTests.Xml(G.S)==before&&Tab=="进化","old receipt and source are inert");
            GuideClick(GuidePrimary());CrystalPreview(root,"evolved-"+pet,size);check(SmallEvolutionStage(pet)==1&&G.S.Crystals==crystals-(pet==0?2:1),"separate deliberate action spends evolution cost once");
        }

        CrystalFixture(4,2);CrystalPreview(root,"late-seed-source");GuideClick(GuidePrimary());CrystalPreview(root,"late-seed-preparation");GuideClick(GuidePrimary());G.AdvanceJourney(DateTime.UtcNow.AddHours(1));CrystalPreview(root,"late-seed-receipt");
        check(G.S.Crystals==2&&GuideWords().Contains("还差 3 颗"),"later seed stage reports actual remaining cost rather than claiming ready");
        string path=Path.Combine(root,"crystal-journey-save.xml");G.S.LastFarmTicks=DateTime.UtcNow.AddMinutes(3).Ticks;XDocument.Parse(FarmPracticeTests.Xml(G.S)).Save(path);
        var game=new FarmGame(path);var window=new FarmWindow(game,true);var panel=new FarmPanel(window);window.Panel=panel;panel.Go("冒险");panel.CrystalPreview(root,"reloaded-receipt",2);
        check(game.R.CrystalTargetPet==4&&game.R.CrystalTargetStage==2&&game.S.CourtyardPet==-1,"isolated normal reload retains specific seed stage without switching courtyard pet");
        panel.GuideClick(panel.GuidePrimary());panel.CrystalPreview(root,"reloaded-requirement",2);check(panel.smallGrowthPet==4&&panel.smallGrowthStep==1&&game.S.SeedStage==2&&game.S.Crystals==2,"reloaded receipt resumes original unmet requirement");
        panel.GuideClick(panel.GuidePrimary());panel.CrystalPreview(root,"retry-preparation",2);panel.GuideClick(panel.GuidePrimary());game.Retreat();panel.CrystalPreview(root,"retreated",2);check(game.R.RewardCrystals==0&&panel.GuideWords().Contains("还差 3 颗"),"retreat never fabricates crystal rewards");
        panel.GuideClick(panel.UIAction("先回小院"));panel.CrystalPreview(root,"alternate-home",2);check(game.AtHome&&panel.Tab=="农场"&&!panel.HasCrystalPreparation,"explicit home return clears run purpose");

        CrystalFixture(0);CrystalPreview(root,"ordinary-source");GuideClick(GuidePrimary());Go("农场");Go("冒险");CrystalPreview(root,"ordinary-preparation");GuideClick(GuidePrimary());check(G.R.CrystalTargetPet==-1,"a later ordinary outing never inherits an abandoned purpose");G.Retreat();G.Home();
        string unchanged=FarmPracticeTests.Xml(G.S);check(!G.BeginJourney(0,0,0,99,0)&&FarmPracticeTests.Xml(G.S)==unchanged,"invalid target cannot charge or start a journey");
        var legacy=XDocument.Load(path);legacy.Root.Element("Run").Element("CrystalTargetPet").Remove();legacy.Root.Element("Run").Element("CrystalTargetStage").Remove();string old=Path.Combine(root,"crystal-journey-legacy.xml");legacy.Save(old);var loaded=new FarmGame(old);check(!loaded.HasCrystalTarget(loaded.R),"older saves gain no invented purpose");
        legacy.Root.Element("Run").Add(new XElement("CrystalTargetPet",999),new XElement("CrystalTargetStage",999));legacy.Save(old);loaded=new FarmGame(old);check(!loaded.HasCrystalTarget(loaded.R),"malformed purpose safely falls back to ordinary receipt");
        CrystalFixture(4);CrystalPreview(root,"reset-source");GuideClick(GuidePrimary());CrystalPreview(root,"reset-preparation");var stale=GuidePrimary();G.ResetGuideTest();ResetGuideView();unchanged=FarmPracticeTests.Xml(G.S);stale.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!HasCrystalPreparation&&G.S.Run.CrystalTargetPet==-1&&FarmPracticeTests.Xml(G.S)==unchanged&&G.SavePath==null&&!IsVisible&&!owner.IsVisible,"fresh guide clears target and rejects old controls without native windows or player saves");
        File.WriteAllText(Path.Combine(root,"crystal-journey-regression.txt"),"PASS: read-only crystal preparation, exact companion and stage, optional preparation and cancellation, actual departure and paused production clock, victory and retreat receipts, no invented crystals, explicit return and evolution costs, isolated saved-run reload, old/malformed purpose fallback, abandoned and stale events, three reading sizes with actual viewport bounds, fresh memory-only guide. Detached WPF only.");
    }
}
