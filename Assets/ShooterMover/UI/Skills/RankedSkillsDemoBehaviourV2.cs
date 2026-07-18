#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using ShooterMover.Application.Progression.Skills.Presentation;
using UnityEngine;

namespace ShooterMover.UI.Skills
{
    public sealed class RankedSkillsDemoBehaviourV2 : MonoBehaviour, IRankedSkillsDemoViewV2
    {
        private static RankedSkillsDemoSessionHostV2 host; private RankedSkillsDemoControllerV2 controller; private RankedSkillsScreenSnapshotV2 snapshot; private RankedSkillsDebugStateV2 debug; private bool quoteOpen; private long quoteCost; private int quotePoints; private string quoteCurrency; private Vector2 scroll; private int targetLevel=20; private long targetCredits=10000;
        private void Awake(){if(host==null)host=new RankedSkillsDemoSessionHostV2(new RankedSkillsDemoSessionFactoryV2());controller=new RankedSkillsDemoControllerV2(host.GetOrCreate(RankedSkillsDemoClassV2.Striker,true),this);controller.Open();}
        public void Render(RankedSkillsScreenSnapshotV2 value){snapshot=value;} public void RenderDebug(RankedSkillsDebugStateV2 value){debug=value;} public void ShowRespecQuote(long cost,int points,string currency){quoteOpen=true;quoteCost=cost;quotePoints=points;quoteCurrency=currency;} public void CloseRespecConfirmation(){quoteOpen=false;}
        private void OnGUI()
        {
            if(snapshot==null)return;GUILayout.BeginArea(new Rect(20,20,Mathf.Min(980,Screen.width-40),Screen.height-40),GUI.skin.box);GUILayout.Label("RANKED SKILLS / RESPEC DEMO");GUILayout.Label($"{snapshot.ClassId} | Level {snapshot.PlayerLevel} | Points {snapshot.AvailablePoints}/{snapshot.TotalPoints} | Credits {snapshot.CreditBalance}");GUILayout.Label("Catalog: "+snapshot.CatalogFingerprint);if(!string.IsNullOrEmpty(snapshot.Feedback))GUILayout.Label("Result: "+snapshot.Feedback);
            GUILayout.BeginHorizontal();if(GUILayout.Button("Striker"))controller.ChangeClass(RankedSkillsDemoClassV2.Striker);if(GUILayout.Button("Combat Medic"))controller.ChangeClass(RankedSkillsDemoClassV2.CombatMedic);if(GUILayout.Button("Juggernaut"))controller.ChangeClass(RankedSkillsDemoClassV2.Juggernaut);if(GUILayout.Button("Full Respec"))controller.QuoteFullRespec();GUILayout.EndHorizontal();
            if(debug!=null&&debug.ControlsAvailable){GUILayout.BeginHorizontal(GUI.skin.box);int.TryParse(GUILayout.TextField(targetLevel.ToString(),GUILayout.Width(70)),out targetLevel);if(GUILayout.Button("Apply level"))controller.ApplyTargetLevel(targetLevel);long.TryParse(GUILayout.TextField(targetCredits.ToString(),GUILayout.Width(100)),out targetCredits);if(GUILayout.Button("Set credits"))controller.SetDemoCredits(targetCredits);if(GUILayout.Button("Fresh profile"))controller.ResetDemoProfile();GUILayout.EndHorizontal();GUILayout.Label(debug.ResetNotice);}
            scroll=GUILayout.BeginScrollView(scroll);foreach(var synergy in snapshot.Synergies){string progress=string.Join(", ",synergy.Requirements.Select(x=>x.Id+" "+x.Current+"/"+x.Required));GUILayout.Label("SYNERGY "+synergy.DisplayName+" — "+(synergy.Active?"ACTIVE":"INACTIVE")+" — "+progress,GUI.skin.box);if(!string.IsNullOrWhiteSpace(synergy.Description))GUILayout.Label(synergy.Description);}
            foreach(var card in snapshot.Cards){GUILayout.BeginVertical(GUI.skin.box);GUILayout.BeginHorizontal();GUILayout.Label(card.DisplayName+" ["+card.SkillId+"]",GUILayout.Width(420));GUILayout.Label($"Rank {card.CurrentRank}/{card.MaximumRank}",GUILayout.Width(100));GUILayout.Label(card.State.ToString(),GUILayout.Width(80));GUI.enabled=card.State!=RankedSkillCardStateV2.Locked&&card.State!=RankedSkillCardStateV2.Capped&&snapshot.AvailablePoints>0;if(GUILayout.Button("+1 rank",GUILayout.Width(90)))controller.Allocate(card.SkillId);GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.Label(card.Description);GUILayout.Label($"Current {card.CurrentValue} → Next {card.NextValue}");foreach(var r in card.Prerequisites)GUILayout.Label($"Prerequisite {r.Id}: {r.Current}/{r.Required}");foreach(var g in card.CategoryGates)GUILayout.Label($"Category gate {g.Id}: {g.Current}/{g.Required}");GUILayout.EndVertical();}GUILayout.EndScrollView();
            if(quoteOpen){GUILayout.BeginVertical(GUI.skin.box);GUILayout.Label($"Refund {quotePoints} points for {quoteCost} {quoteCurrency}?");GUILayout.BeginHorizontal();if(GUILayout.Button("Confirm"))controller.ConfirmFullRespec();if(GUILayout.Button("Cancel"))controller.CancelFullRespec();GUILayout.EndHorizontal();GUILayout.EndVertical();}GUILayout.EndArea();
        }
    }
}
#endif
