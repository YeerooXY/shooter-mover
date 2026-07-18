using System;
using ShooterMover.Application.Progression.Skills.Presentation;

namespace ShooterMover.UI.Skills
{
    public interface IRankedSkillsDemoViewV2
    {
        void Render(RankedSkillsScreenSnapshotV2 snapshot);
        void RenderDebug(RankedSkillsDebugStateV2 debugState);
        void ShowRespecQuote(long exactCost,int refundablePoints,string currencyId);
        void CloseRespecConfirmation();
    }

    public sealed class RankedSkillsDemoControllerV2
    {
        private readonly IRankedSkillsDemoSessionV2 session; private readonly IRankedSkillsDemoViewV2 view; private ShooterMover.Application.Progression.Skills.SkillRespecQuoteV2 pendingQuote;
        public RankedSkillsDemoControllerV2(IRankedSkillsDemoSessionV2 session,IRankedSkillsDemoViewV2 view){this.session=session??throw new ArgumentNullException(nameof(session));this.view=view??throw new ArgumentNullException(nameof(view));}
        public void Open()=>Render();
        public void Allocate(string skillId){var before=session.Screen.Refresh();var result=session.Screen.Allocate(RankedSkillsDebugOperationIdsV2.Allocate(session.ProfileId,skillId,before.AllocationVersion),skillId,before.AllocationVersion);view.Render(session.Screen.Refresh(result.Accepted?"Allocated":result.Rejection.ToString()));}
        public void QuoteFullRespec(){pendingQuote=session.Screen.QuoteFullRespec();view.ShowRespecQuote(pendingQuote.ExactCost,pendingQuote.AllocatedPoints,pendingQuote.CurrencyId);}
        public void ConfirmFullRespec(){if(pendingQuote==null){view.Render(session.Screen.Refresh("No active respec quote"));return;}var receipt=session.Screen.ConfirmFullRespec(RankedSkillsDebugOperationIdsV2.Respec(session.ProfileId,pendingQuote.Fingerprint),pendingQuote);pendingQuote=null;view.CloseRespecConfirmation();view.Render(session.Screen.Refresh(receipt.Accepted?"Respec complete":receipt.Rejection.ToString()));}
        public void CancelFullRespec(){pendingQuote=null;view.CloseRespecConfirmation();Render();}
        public void ApplyTargetLevel(int value){session.ApplyTargetLevel(value);Render();} public void SetDemoCredits(long value){session.SetDemoCredits(value);Render();}
        public void ChangeClass(RankedSkillsDemoClassV2 value){session.ChangeClass(value);pendingQuote=null;Render();} public void ResetDemoProfile(){session.ResetProfile();pendingQuote=null;Render();}
        private void Render(){view.Render(session.Screen.Refresh());view.RenderDebug(session.DebugState);}
    }
}
