using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace SolutionGenerator.Toolkit
{
	public static class Extensions
	{
		#region XML SERIALIZE

		private static readonly string UTF8Preamble = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

		public static string XmlSerialize(this object objectToSerialize, bool internalXml = false, string defaultNamespace = "")
		{
			if (objectToSerialize == null)
				throw new ArgumentNullException("objectToSerialize");

			string serializedObject = objectToSerialize as string;

			if (serializedObject != null)
				return serializedObject;

			Type objectType = objectToSerialize.GetType();

			using (MemoryStream memoryStream = new MemoryStream())
			{
				XmlWriterSettings settings = new XmlWriterSettings {Encoding = Encoding.UTF8, OmitXmlDeclaration = internalXml};

				if (!internalXml)
				{
					settings.Indent = true;
					settings.IndentChars = "\t";
					settings.NewLineHandling = NewLineHandling.Entitize;
					settings.NewLineChars = Environment.NewLine;
				}

				using (XmlWriter xmlWriter = XmlWriter.Create(memoryStream, settings))
				{
					XmlSerializer serializer = new XmlSerializer(objectType);

					XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
					if (!string.IsNullOrEmpty(defaultNamespace))
						namespaces.Add("", defaultNamespace);
					else
						namespaces.Add("", "");

					serializer.Serialize(xmlWriter, objectToSerialize, namespaces);

					serializedObject = Encoding.UTF8.GetString(memoryStream.ToArray());

					//remove BOM mark
					if (!string.IsNullOrEmpty(UTF8Preamble) && serializedObject.StartsWith(UTF8Preamble))
						serializedObject = serializedObject.Substring(UTF8Preamble.Length);
				}
			}

			return serializedObject;
		}

		public static T XmlDeserialize<T>(this string objectToDeserialize)
		{
			if (string.IsNullOrWhiteSpace(objectToDeserialize))
				throw new ArgumentNullException("objectToDeserialize");

			object deserializedObject;

			Type objectType = typeof (T);
			if (objectType == typeof (string))
			{
				deserializedObject = objectToDeserialize;
			}
			else
			{
				using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(objectToDeserialize)))
				{
					using (XmlTextReader xmlReader = new XmlTextReader(memoryStream))
					{
						XmlSerializer serializer = new XmlSerializer(objectType);
						deserializedObject = serializer.Deserialize(xmlReader);
					}
				}
			}
			return (T) deserializedObject;
		}

		internal static T XmlDeserialize<T>(this FileSystemInfo fileInfo)
		{
			if (!fileInfo.Exists)
				return default(T);

			object deserializedObject;

			Type objectType = typeof (T);
			if (objectType == typeof (string))
			{
				deserializedObject = File.ReadAllText(fileInfo.FullName);
			}
			else
			{
				using (FileStream memoryStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					using (XmlTextReader xmlReader = new XmlTextReader(memoryStream))
					{
						XmlSerializer serializer = new XmlSerializer(objectType);
						deserializedObject = serializer.Deserialize(xmlReader);
					}
				}
			}
			return (T) deserializedObject;
		}

		#endregion

		public static void SafeInvoke(this Control control, Action action)
		{
			if (control.InvokeRequired)
			{
				if (!control.Disposing && control.IsHandleCreated)
				{
					MethodInvoker methodInvoker = () => SafeInvoke(control, action);
					control.Invoke(methodInvoker);
				}
			}
			else
			{
				if (!control.Disposing && control.IsHandleCreated)
				{
					action.Invoke();
				}
			}
		}
	}
}
