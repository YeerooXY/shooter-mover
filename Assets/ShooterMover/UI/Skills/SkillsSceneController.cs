using System;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Skills
{
    public sealed class SkillsSceneController : MonoBehaviour
    {
        [SerializeField, Range(1, 100)] private int previewPlayerLevel = 20;
        [SerializeField] private string backSceneName = "MainMenu";
        private SkillProgressionAuthorityV1 authority;
        private Vector2 scroll;
        private long operationSequence;

        private void Awake()
        {
            authority = new SkillProgressionAuthorityV1(SkillCatalogV1.CreateDefault(), previewPlayerLevel);
        }

        private void OnGUI()
        {
            var snapshot = authority.CurrentSnapshot;
            GUI.Box(new Rect(20, 16, Screen.width - 40, 64), "SKILLS");
            GUI.Label(new Rect(40, 48, 350, 24), "Level " + snapshot.PlayerLevel + "   Available points: " + snapshot.AvailablePoints);
            if (GUI.Button(new Rect(Screen.width - 150, 34, 100, 30), "Back"))
            {
                if (!string.IsNullOrWhiteSpace(backSceneName)) SceneManager.LoadScene(backSceneName);
            }

            Rect viewport = new Rect(20, 92, Screen.width - 40, Screen.height - 112);
            Rect content = new Rect(0, 0, viewport.width - 20, 5 * 142);
            scroll = GUI.BeginScrollView(viewport, scroll, content);
            int index = 0;
            foreach (var definition in authority.Catalog.Definitions)
            {
                int column = index % 4;
                int row = index / 4;
                float width = (content.width - 30) / 4f;
                Rect card = new Rect(column * (width + 10), row * 142, width, 132);
                GUI.Box(card, string.Empty);
                GUI.Label(new Rect(card.x + 10, card.y + 8, card.width - 20, 22), definition.DisplayName);
                int rank = snapshot.Ranks[definition.Id];
                GUI.Label(new Rect(card.x + 10, card.y + 34, card.width - 20, 22), "Rank " + rank + " / " + definition.MaxRank);
                GUI.Label(new Rect(card.x + 10, card.y + 56, card.width - 20, 40), definition.Description);
                if (GUI.Button(new Rect(card.x + 10, card.y + 100, card.width - 20, 24), "Spend point"))
                {
                    operationSequence++;
                    authority.Allocate("skills-scene-" + operationSequence, definition.Id);
                }
                index++;
            }
            GUI.EndScrollView();
        }
    }
}