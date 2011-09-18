using System;
using System.IO;

namespace MaxMind.GeoIP
{
	public class Region
	{
		public String countryCode;
		public String countryName;
		public String region;
		public Region()
		{
		}
		public Region(String countryCode, String countryName, String region)
		{
			this.countryCode = countryCode;
			this.countryName = countryName;
			this.region = region;
		}
		public String getcountryCode()
		{
			return countryCode;
		}
		public String getcountryName()
		{
			return countryName;
		}
		public String getregion()
		{
			return region;
		}
	}
}
