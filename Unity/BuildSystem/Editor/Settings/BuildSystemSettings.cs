using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BuildSystem.Processes;
using BuildSystem.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace BuildSystem.Settings
{
	public class BuildSystemSettings : SettingsProvider
	{
		public BuildSystemSettings(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
		{
		}
		
		[SettingsProvider]
		public static SettingsProvider CreateMyCustomSettingsProvider()
		{
			return new BuildSystemSettings("Project/Build System Settings", SettingsScope.Project)
			{
				// keywords = BuildConfig.GetKeywords()
			};
		}

		private static PipelineConfig GetOrCreateSettings()
		{
			var settings = AssetFinder.GetAsset<PipelineConfig>();

			if (settings)
				return settings;
			
			settings = ScriptableObject.CreateInstance<PipelineConfig>();
			AssetDatabase.CreateAsset(settings, "Assets/Settings/BuildSettings/PipelineConfig.asset");
			AssetDatabase.SaveAssets();
			return settings;
		}

		public override void OnActivate(string searchContext, VisualElement rootElement)
		{
			base.OnActivate(searchContext, rootElement);
			
			var config = GetOrCreateSettings();
			var serialisedSettings = new SerializedObject(config);
			GetElementsFromFields(config, serialisedSettings, rootElement);
			rootElement.Bind(serialisedSettings);
			
			var processChoices = Assembly.GetExecutingAssembly()
				.GetTypes()
				.Where(t => t.IsSubclassOf(typeof(PipelineProcess)) && !t.IsAbstract)
				.Select(t => t.Name);

			var dropdownField = new DropdownField("Add New Process", new List<string>(processChoices), 0);
			dropdownField.RegisterValueChangedCallback(evt =>
			{
				Debug.Log($"Add new process: {evt.newValue}");
				// config.Processes.Add(new );
			});
			rootElement.Add(dropdownField);
		}
		
		private static void GetElementsFromFields(object obj, SerializedObject serializedObject, VisualElement rootElement)
		{
			var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

			foreach (var fieldInfo in fields)
			{
				var field = new PropertyField(serializedObject.FindProperty(fieldInfo.Name));
				rootElement.Add(field);
			}
		}
	}
}