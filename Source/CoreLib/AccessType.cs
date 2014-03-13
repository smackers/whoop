using System;

namespace whoop
{
  public sealed class AccessType {

    readonly String name;

    public static readonly AccessType READ = new AccessType("READ");
    public static readonly AccessType WRITE = new AccessType("WRITE");

    public static AccessType Create(string access) {
      if(access.ToUpper() == "READ") {
        return READ;
      }
      if(access.ToUpper() == "WRITE") {
        return WRITE;
      }
      throw new NotSupportedException("Unknown access type: " + access);
    }

    private AccessType(String name) {
      this.name = name;
    }

    public override String ToString() {
      return name;
    }

    public bool IsRead() {
      return name.Equals("READ");
    }

    public bool IsWrite() {
      return name.Equals("WRITE");
    }
  }
}

