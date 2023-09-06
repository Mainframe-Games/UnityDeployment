using System.Collections.Generic;
using UnityEngine;

namespace BuildSystem.Processes
{
	public class PipelineConfig : ScriptableObject
	{
		public string Name;
		public string[] Names;
		public List<PipelineProcess> Processes;
		public PipelineProcess[] Processes2;
	}
}