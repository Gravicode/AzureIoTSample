// Import the interface to Tessel hardware
'use strict';
var tessel = require('tessel');
var climatelib = require('climate-si7020');
var climate = climatelib.use(tessel.port['A']);

 var clientFromConnectionString = require('azure-iot-device-mqtt').clientFromConnectionString;
 var Message = require('azure-iot-device').Message;
 var connectionString = 'HostName=FreeDeviceHub.azure-devices.net;DeviceId=TesselSensor;SharedAccessKey=JKH3ffH7DW2FSzrHvS3hAls//1ePHkX/g/AVfo4RY20=';

 var client = clientFromConnectionString(connectionString);
var sensorReady = false;

climate.on('ready', function () {
  console.log('Connected to climate module');
  sensorReady =true;
  
});
function toCelsius(f) {
    return (5/9) * (f-32);
}
climate.on('error', function(err) {
  console.log('error connecting module', err);
});

function printResultFor(op) {
   return function printResult(err, res) {
     if (err) console.log(op + ' error: ' + err.toString());
     if (res) console.log(op + ' status: ' + res.constructor.name);
   };
 }

 var connectCallback = function (err) {
   if (err) {
     console.log('Could not connect: ' + err);
   } else {
     console.log('Client connected');

     // Create a message and send it to the IoT Hub every second
     setInterval(function(){
       if(sensorReady){
        climate.readTemperature('f', function (err, temp) {
              climate.readHumidity(function (err, humid) {
                tessel.led[2].toggle();
                tessel.led[3].toggle();
                var data = JSON.stringify({ Dev: 'Tessel', Celcius: toCelsius(temp.toFixed(4)), Humidity:humid.toFixed(4), Geo : 'Indonesia'  });
                var message = new Message(data);
                console.log("Sending message: " + message.getData());
                client.sendEvent(message, printResultFor('send'));

              });
            });
          }
     }, 5000);
   }
 };

client.open(connectCallback);
// Turn one of the LEDs on to start.
tessel.led[2].on();


