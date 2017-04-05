#include <dht11.h>


dht11 DHT11;

const int DHT11PIN = 2; // GPIO pin 2 = D4 alias on NodeMCU, using gpio pin rather than D4 alias so it compiles against all ESP8266 Development boards.

void initDHT11(){
  int chk = DHT11.read(DHT11PIN);
}

void getDht11Readings(){
  int retryCount = 0;
  initDHT11();

  do {
    delay(50);
    data.temperature = (float)DHT11.temperature;
    data.humidity = (float)DHT11.humidity;
  } while ((isnan(data.temperature) || isnan(data.humidity)) && ++retryCount < 10);
}
