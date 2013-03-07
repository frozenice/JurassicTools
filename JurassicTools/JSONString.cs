using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JurassicTools
{
  public class JSONString
  {
    internal readonly string plainString;

    public JSONString(string s)
    {
      plainString = s;
    }

    public override string ToString()
    {
      return plainString;
    }

    public static implicit operator string(JSONString jsonString)
    {
      return jsonString.plainString;
    }

    public static implicit operator JSONString(string str)
    {
      return new JSONString(str);
    }
  }
}
