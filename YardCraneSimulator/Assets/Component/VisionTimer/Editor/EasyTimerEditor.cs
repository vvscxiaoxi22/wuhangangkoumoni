using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EasyTimer))]


public class EasyTimerEditor : Editor
{

	private bool m_ShowId = false;
	private bool m_ShowCallStack = false;
	private static GUIStyle m_SmallTextStyle = null;

	public static GUIStyle SmallTextStyle
	{
		get
		{
			if (m_SmallTextStyle == null)
			{
				m_SmallTextStyle = new GUIStyle("Label");
				m_SmallTextStyle.fontSize = 9;
				m_SmallTextStyle.alignment = TextAnchor.LowerLeft;
				m_SmallTextStyle.padding = new RectOffset(0, 0, 4, 0);
			}
			return m_SmallTextStyle;
		}
	}


	/// <summary>
	/// 
	/// </summary>
	private void OnEnable()
	{

        EasyTimer timer = (EasyTimer)target;

		if(!timer.WasAddedCorrectly)
		{
			EditorUtility.DisplayDialog("警告!", "计时器管理脚本不能添加到面板，请查看使用说明", "OK");
			DestroyImmediate(timer);
		}

	}


	/// <summary>
	/// 
	/// </summary>
	public override void OnInspectorGUI()
	{

		EditorGUILayout.Space();

        EasyTimer.Stats stats = EasyTimer.EditorGetStats();
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.BeginVertical();
		EditorGUILayout.LabelField("\tCreated: " + stats.Created);
		EditorGUILayout.LabelField("\tInactive: " + stats.Inactive);
		EditorGUILayout.LabelField("\tActive: " + stats.Active);
		EditorGUILayout.EndVertical();
		EditorGUILayout.BeginVertical();
		EditorGUILayout.BeginHorizontal();
		m_ShowId = GUILayout.Toggle(m_ShowId, "", GUILayout.MaxWidth(12));
		GUILayout.Label("Show Id", SmallTextStyle);
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.BeginHorizontal();
		m_ShowCallStack = GUILayout.Toggle(m_ShowCallStack, "", GUILayout.MaxWidth(12));
		GUILayout.Label("Show CallStack", SmallTextStyle);
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndHorizontal();
		
		EditorGUILayout.Space();

		if (stats.Active > 100)
		{
			EditorGUILayout.HelpBox( "已经存在很多计时器对象，考虑性能只显示最近的100条数据", MessageType.Warning);
		}

		for (int c = Mathf.Min(100, stats.Active - 1); c > -1; c--)
		{

			EditorGUILayout.BeginHorizontal();

			string s = "\t";

			if(m_ShowId)
			    s += EasyTimer.EditorGetMethodId(c) + ": ";

			s += EasyTimer.EditorGetMethodInfo(c);

			if (!m_ShowCallStack)
			{
				int a = s.IndexOf(" <");
				s = s.Substring(0, a);
			}

			EditorGUILayout.HelpBox(s, MessageType.None);
			EditorGUILayout.EndHorizontal();

		}

	}
	
	
}

