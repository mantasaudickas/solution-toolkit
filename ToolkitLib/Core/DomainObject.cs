using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SolutionGenerator.Toolkit.Core
{
	[Serializable]
	public class DomainObject : ICloneable
	{
		object ICloneable.Clone()
		{
			return this.Clone();
		}

		public virtual DomainObject Clone()
		{
			DomainObject clone;

			BinaryFormatter formatter = new BinaryFormatter();
			using (MemoryStream stream = new MemoryStream())
			{
				formatter.Serialize(stream, this);
				stream.Position = 0;
				clone = (DomainObject) formatter.Deserialize(stream);
			}

			return clone;
		}
	}
}
