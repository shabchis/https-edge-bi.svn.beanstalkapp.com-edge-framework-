using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;

namespace Edge.Data.Objects
{
	public static class EdgeObjects
	{
		public static EntitySpace EntitySpace { get; private set; }

		static EdgeObjects()
		{
			EdgeObjects.EntitySpace = new EntitySpace();
		}

		public static string QueryTemplateText(string fileName, string templateName)
		{
			const string tplSeparatorPattern = @"^--\s*#\s*TEMPLATE\s+(.*)$";
			Regex tplSeparatorRegex = new Regex(tplSeparatorPattern, RegexOptions.Singleline);

			var templateChars = new StringBuilder();
			Assembly asm = Assembly.GetExecutingAssembly();
			using (StreamReader reader = new StreamReader(asm.GetManifestResourceStream(@"Edge.Data.Objects.Queries." + fileName)))
			{
				bool readingTemplate = false;
				
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();
					if (!readingTemplate)
					{
						Match m = tplSeparatorRegex.Match(line);
						if (m.Success && m.Groups[1].Value.Trim() == templateName)
							readingTemplate = true;
					}
					else
					{
						if (tplSeparatorRegex.IsMatch(line))
							break;
						else
							templateChars.Append(line);
					}

				}
			}

			if (templateChars.Length == 0)
				throw new Exception("Template not found in resource.");

			return templateChars.ToString();
		}
	}
}
