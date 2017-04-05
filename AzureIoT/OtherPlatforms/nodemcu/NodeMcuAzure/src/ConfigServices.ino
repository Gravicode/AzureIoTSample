#include <EEPROM.h>
/*
void initCloudConfig() {
	EEPROM.begin(512);
	char* data;
	int length;

	const int BUFFER_SIZE = JSON_OBJECT_SIZE(4) + JSON_ARRAY_SIZE(0);
	StaticJsonBuffer<1000> jsonBuffer;
	int address = 2;

	length = word(EEPROM.read(0), EEPROM.read(1));
	data = new char[length];

	for (address = 2; address < length + 2; address++) {
		data[address - 2] = EEPROM.read(address);
	}
	data[address - 2] = '\0';


	JsonObject& root = jsonBuffer.parseObject(data);
	if (!root.success())
	{
		Serial.println("parseObject() failed");
		return;
	}

  cloud.host = GetValue(root["host"]);
  cloud.key = (char*)GetValue(root["key"]);
  cloud.id = GetValue(root["id"]);
  cloud.geo = GetValue(root["geo"]);


  device.wifiPairs = root["wifi"];
  device.ssid = new const char*[device.wifiPairs];
  device.pwd = new const char*[device.wifiPairs];

	for (int i = 0; i < device.wifiPairs; i++)
	{
    device.ssid[i] = GetValue(root["ssid"][i]);
    device.pwd[i] = GetValue(root["pwd"][i]);
	}
}
*/


//http://hardwarefun.com/tutorials/url-encoding-in-arduino
String urlEncode(const char* msg)
{
    const char *hex = "0123456789abcdef";
    String encodedMsg = "";

    while (*msg!='\0'){
        if( ('a' <= *msg && *msg <= 'z')
                || ('A' <= *msg && *msg <= 'Z')
                || ('0' <= *msg && *msg <= '9') ) {
            encodedMsg += *msg;
        } else {
            encodedMsg += '%';
            encodedMsg += hex[*msg >> 4];
            encodedMsg += hex[*msg & 15];
        }
        msg++;
    }
    return encodedMsg;
}

// http://arduino.stackexchange.com/questions/1013/how-do-i-split-an-incoming-string
String splitStringByIndex(String data, char separator, int index)
{
  int found = 0;
  int strIndex[] = { 0, -1  };
  int maxIndex = data.length()-1;

  for(int i=0; i<=maxIndex && found<=index; i++){
    if(data.charAt(i)==separator || i==maxIndex){
      found++;
      strIndex[0] = strIndex[1]+1;
      strIndex[1] = (i == maxIndex) ? i+1 : i;
    }
  }
  return found>index ? data.substring(strIndex[0], strIndex[1]) : "";
}


const char *GetValue(const char* value){
  char *temp = new char[strlen(value) + 1];
  strcpy(temp, value);
  return temp;
}

const char *GetStringValue(String value){
  int len = value.length() + 1;
  char *temp = new char[len];
  value.toCharArray(temp, len);
  return temp;
}

void initCloudConfig(String cs, const char *ssid, const char *pwd, const char *geo){
  device.wifiPairs = 1;
  device.ssid = new const char*[device.wifiPairs];
  device.pwd = new const char*[device.wifiPairs];
  device.ssid[0] = ssid;
  device.pwd[0] = pwd;
  cloud.geo = geo;

  cloud.host = GetStringValue(splitStringByIndex(splitStringByIndex(cs, ';', 0), '=', 1));
  cloud.id = GetStringValue(splitStringByIndex(splitStringByIndex(cs, ';', 1), '=', 1));
  cloud.key = (char*)GetStringValue(splitStringByIndex(splitStringByIndex(cs, ';', 2), '=', 1));
}
