using System;
using System.Collections.Generic;
using System.Linq;
using _Game.Components;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace _Game.Gameplay.Waves.Editor
{
    [CustomEditor(typeof(WaveManager))]
    public class WaveManagerEditor : UnityEditor.Editor
    {
        private static readonly RoleMask[] AllTypes = (RoleMask[])Enum.GetValues(typeof(RoleMask));
        private static readonly EnemyTier[] AllTiers = (EnemyTier[])Enum.GetValues(typeof(EnemyTier));

        private SerializedProperty _budgetBase;
        private SerializedProperty _budgetPerWave;
        private SerializedProperty _definitions;
        private ReorderableList _defList;
        private readonly Dictionary<int, ReorderableList> _tierListCache = new();

        private void OnEnable()
        {
            _budgetBase = serializedObject.FindProperty("budgetBase");
            _budgetPerWave = serializedObject.FindProperty("budgetPerWave");
            _definitions = serializedObject.FindProperty("enemyTypeDefinitions");

            _defList = new ReorderableList(serializedObject, _definitions, true, true, true, true)
            {
                drawHeaderCallback = r => EditorGUI.LabelField(r, "Enemy Type Definitions"),
                drawElementCallback = DrawDefinition,
                elementHeightCallback = DefinitionHeight,
                onAddCallback = OnAddDefinition,
                onChangedCallback = _ => _tierListCache.Clear(),
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_budgetBase);
            EditorGUILayout.PropertyField(_budgetPerWave);
            _defList.displayAdd = _definitions.arraySize < AllTypes.Length;
            _defList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            if (_definitions.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No enemy types defined. Add at least one type.", MessageType.Warning);
            }
            else
            {
                List<string> emptyTypes = new();
                for (int i = 0; i < _definitions.arraySize; i++)
                {
                    SerializedProperty def = _definitions.GetArrayElementAtIndex(i);
                    if (def.FindPropertyRelative("tiers").arraySize == 0)
                    {
                        emptyTypes.Add(((RoleMask)def.FindPropertyRelative("type").intValue).ToString());
                    }
                }

                if (emptyTypes.Count > 0)
                {
                    EditorGUILayout.HelpBox($"{string.Join(", ", emptyTypes)} has no tier entries.", MessageType.Warning);
                }
            }
        }

        // ─── Definition list ───────────────────────────────────────────────
        private void DrawDefinition(Rect rect, int index, bool active, bool focused)
        {
            rect.y += 2f;
            float lh = EditorGUIUtility.singleLineHeight;
            SerializedProperty def = _definitions.GetArrayElementAtIndex(index);
            SerializedProperty typeProp = def.FindPropertyRelative("type");

            RoleMask current = (RoleMask)typeProp.intValue;
            RoleMask[] available = AllTypes.Where(t => t == current || !TypeUsedElsewhere(t, index)).ToArray();
            int sel = EditorGUI.Popup(new Rect(rect.x, rect.y, rect.width, lh), "Type", Array.IndexOf(available, current), available.Select(t => t.ToString()).ToArray());
            typeProp.intValue = (int)available[Mathf.Clamp(sel, 0, available.Length - 1)];

            Rect tiersRect = new(rect.x, rect.y + lh + 4f, rect.width, rect.height - lh - 4f);
            GetOrCreateTierList(def, index).DoList(tiersRect);
        }

        private float DefinitionHeight(int index)
        {
            SerializedProperty def = _definitions.GetArrayElementAtIndex(index);
            float lh = EditorGUIUtility.singleLineHeight;
            return lh + 4f + GetOrCreateTierList(def, index).GetHeight() + 4f;
        }

        private void OnAddDefinition(ReorderableList list)
        {
            if (_definitions.arraySize >= AllTypes.Length) return;

            _definitions.arraySize++;
            list.index = _definitions.arraySize - 1;

            SerializedProperty def = _definitions.GetArrayElementAtIndex(list.index);
            RoleMask unused = AllTypes.First(t => !TypeUsedElsewhere(t, list.index));
            def.FindPropertyRelative("type").intValue = (int)unused;
            def.FindPropertyRelative("tiers").ClearArray();

            serializedObject.ApplyModifiedProperties();
            _tierListCache.Clear();
        }

        private bool TypeUsedElsewhere(RoleMask type, int excludeIndex)
        {
            for (int i = 0; i < _definitions.arraySize; i++)
            {
                if (i == excludeIndex) continue;

                if ((RoleMask)_definitions.GetArrayElementAtIndex(i).FindPropertyRelative("type").intValue == type)
                {
                    return true;
                }
            }

            return false;
        }

        // ─── Tier list ─────────────────────────────────────────────────────
        private ReorderableList GetOrCreateTierList(SerializedProperty def, int defIndex)
        {
            SerializedProperty tiers = def.FindPropertyRelative("tiers");
            if (!_tierListCache.TryGetValue(defIndex, out ReorderableList list))
            {
                list = new ReorderableList(serializedObject, tiers, true, true, true, true);
                _tierListCache[defIndex] = list;
            }

            list.serializedProperty = tiers;
            list.displayAdd = tiers.arraySize < AllTiers.Length;
            list.drawHeaderCallback = DrawTierHeader;
            list.drawElementCallback = (r, i, _, _) => DrawTier(r, list.serializedProperty, i);
            list.elementHeightCallback = _ => EditorGUIUtility.singleLineHeight + 4f;
            list.onAddCallback = OnAddTier;

            return list;
        }

        private static void DrawTierHeader(Rect r)
        {
            float w = r.width / 4f;
            EditorGUI.LabelField(new Rect(r.x, r.y, w, r.height), "Tier");
            EditorGUI.LabelField(new Rect(r.x + w, r.y, w, r.height), "Cost");
            EditorGUI.LabelField(new Rect(r.x + w * 2f, r.y, w, r.height), "Weight");
            EditorGUI.LabelField(new Rect(r.x + w * 3f, r.y, w, r.height), "Start Wave");
        }

        private static void DrawTier(Rect rect, SerializedProperty tiers, int index)
        {
            rect.y += 2f;
            float w = rect.width / 4f;
            float h = EditorGUIUtility.singleLineHeight;

            SerializedProperty entry = tiers.GetArrayElementAtIndex(index);
            SerializedProperty tierProp = entry.FindPropertyRelative("tier");
            SerializedProperty costProp = entry.FindPropertyRelative("cost");
            SerializedProperty weightProp = entry.FindPropertyRelative("weight");
            SerializedProperty waveProp = entry.FindPropertyRelative("startWave");

            EnemyTier current = (EnemyTier)tierProp.intValue;
            EnemyTier[] available = AllTiers.Where(t => t == current || !TierUsedElsewhere(t, tiers, index)).ToArray();
            int sel = EditorGUI.Popup(new Rect(rect.x, rect.y, w - 4f, h), Array.IndexOf(available, current), available.Select(t => t.ToString()).ToArray());
            tierProp.intValue = (int)available[Mathf.Clamp(sel, 0, available.Length - 1)];

            costProp.intValue = Mathf.Max(1, EditorGUI.IntField(new Rect(rect.x + w, rect.y, w - 4f, h), costProp.intValue));
            weightProp.intValue = Mathf.Max(1, EditorGUI.IntField(new Rect(rect.x + w * 2f, rect.y, w - 4f, h), weightProp.intValue));
            waveProp.intValue = Mathf.Max(1, EditorGUI.IntField(new Rect(rect.x + w * 3f, rect.y, w - 4f, h), waveProp.intValue));
        }

        private void OnAddTier(ReorderableList list)
        {
            SerializedProperty tiers = list.serializedProperty;
            if (tiers.arraySize >= AllTiers.Length) return;

            tiers.arraySize++;
            list.index = tiers.arraySize - 1;

            SerializedProperty entry = tiers.GetArrayElementAtIndex(list.index);
            entry.FindPropertyRelative("cost").intValue = 1;
            entry.FindPropertyRelative("weight").intValue = 0;
            entry.FindPropertyRelative("startWave").intValue = 1;

            EnemyTier unused = AllTiers.First(t => !TierUsedElsewhere(t, tiers, list.index));
            entry.FindPropertyRelative("tier").intValue = (int)unused;

            serializedObject.ApplyModifiedProperties();
        }

        private static bool TierUsedElsewhere(EnemyTier tier, SerializedProperty tiers, int excludeIndex)
        {
            for (int i = 0; i < tiers.arraySize; i++)
            {
                if (i == excludeIndex) continue;
                
                if ((EnemyTier)tiers.GetArrayElementAtIndex(i).FindPropertyRelative("tier").intValue == tier)
                {
                    return true;
                }
            }

            return false;
        }
    }
}