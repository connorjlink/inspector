# MQTT INSPECTOR
MQTT Inspector (_inspector_) is a C#/WPF-based Windows desktop application used to monitor and transmit MQTT traffic. Connect to a broker (with or without TLS encryption as selected), and simply subscribe to any topics to view the incoming messages. All logged messages are displayed in the "All Messages Tab", whereas the most recently received message for each topic is shown in the "Live Messages" tab. As desired, publish messages out on a specified topic (and Quality of Service) with a given message payload and format (string representation, binary, or google/protobuf-encoded) to transmit. Messages can be configured to send periodically at a desired rate.


### COMMAND CONSOLE
_inspector_ is built around automation and features a custom command processing console. The functions currently supported are:
- set (property-name) (value)
  > Configure the CA Cert, Client Cert, and Private Key
- enabletls
  > Enable TLS support
- disabletls
  > Disable TLS support
- connect (ip-address):(port)
  > Connect to an MQTT broker on the given IP address and port
- disconnect
  > Disconnect from the currently connected broker
- subscribe (topic):(quality-of-service)
  > Subscribe to the given topic at the specified QoS level (0, 1, 2)
- unsubscribe (topic)
  > Unsubscribe from the given topic
- start (topic):(quality-of-service) "(payload)" (format) @(interval)ms
  > Start transmitting the given topic at the specfied QoS level (0, 1, 2) with the specified payload and given format (0-string, 1-binary, 2-protobuf) repeatedly at the specified interval in milliseconds
- stop (topic)
  > Stop transmitting the given topic repeatedly
- publish (topic):(quality-of-service) "(payload)" (format)
  > Transmit a single message for the given topic at the specified QoS level (0, 1, 2) with the specified payload and given format (0-string, 1-binary, 2-protobuf)
- pause (topic)
  > Pause periodic transmission of the given topic
- resume (topic)
  > Resume periodic transmission of the given topic
- pauseall
  > Pause periodic transmission of every active topic
- resumeall
  > Resume periodic transmission of every active topic
- killall
  > Stop transmitting every message that was started as periodic
- silence
  > Rescind any active yellow error notifications
- man/help
  > Get directed to this page :)
