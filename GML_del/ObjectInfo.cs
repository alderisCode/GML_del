/*
 * Created by SharpDevelop.
 * User: user
 * Date: 29.07.2022
 * Time: 10:14
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.ObjectModel;

namespace GML_del
{
	/// <summary>
	/// Description of ObjectInfo.
	/// </summary>
	public class ObjectInfo
	{
		
		public string Type;
		public int LineNr;
		public string lokalnyId;
		public int LineStart;
		public int LineEnd;
		public bool Archived = false;
		public bool Deleted = false;
		public bool ObKarto = false;
		public DateTime StartOb;
		public DateTime StartWersjiOb;
		public DateTime KoniecOb;
		public DateTime KoniecWersjiOb;
		public Collection<RefInfo> references;
		public bool ToRemove = false;
		public string Angle = "-";					// wartość dla braku kąta obrotu w obiekcie
		public bool RotateAngle = false;
		public int AngleLine;
		public DateTime DataPomiaru;
		public char Source;
		
		public ObjectInfo()
		{
			references = new Collection<RefInfo>();
		}
	}

	public class ObjectType
    {
		public string Type;
		public int Count;

		public ObjectType(string objectType, int count)
        {
			Type = objectType;
			Count = count;
        }

    }


	public class RefInfo
    {
		public int LineNr;
		public string lokalnyId;
		public bool isOk;

		public RefInfo(int lineNr, string locId, bool is_Ok)
        {
			LineNr = lineNr;
			lokalnyId = locId;
			isOk = is_Ok;
        }
    }
}
