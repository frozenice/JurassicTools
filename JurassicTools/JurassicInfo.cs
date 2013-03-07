using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JurassicTools
{
  public class JurassicInfo
  {
    public string MemberName { get; set; }
    public Attribute[] Attributes { get; set; }

    public JurassicInfo(string name, params Attribute[] attributes)
    {
      MemberName = name;
      Attributes = attributes;
    }
  }
}
