// AI Agent Beacon — Arduino Nano Firmware
// Reads single-char serial commands (W, D, C) and drives two indicator lights.
// Red light = Waiting (AI agent needs attention)
// Green light = Done (AI agent finished)
//
// Hardware: Arduino Nano + two LED indicator lights (e.g. B09L7XD5QY)
//   Red light  → pin 5 (via transistor/MOSFET if 12V)
//   Green light → pin 6 (via transistor/MOSFET if 12V)

#include <Arduino.h>

#define RED_PIN   5
#define GREEN_PIN 7

#define PULSE_ON_MS  500
#define PULSE_OFF_MS 500

enum State { STATE_IDLE, STATE_WAITING, STATE_DONE };
State currentState = STATE_IDLE;
unsigned long lastToggleMs = 0;
bool pulseOn = false;

void setup() {
  Serial.begin(9600);
  pinMode(RED_PIN, OUTPUT);
  pinMode(GREEN_PIN, OUTPUT);
  digitalWrite(RED_PIN, LOW);
  digitalWrite(GREEN_PIN, LOW);
}

void loop() {
  if (Serial.available()) {
    char cmd = Serial.read();
    switch (cmd) {
      case 'H': Serial.println("OK"); break;
      case 'W': currentState = STATE_WAITING; pulseOn = true; lastToggleMs = millis(); break;
      case 'D': currentState = STATE_DONE;    pulseOn = true; lastToggleMs = millis(); break;
      case 'C': currentState = STATE_IDLE;    break;
    }
  }

  unsigned long now = millis();
  unsigned long interval = pulseOn ? PULSE_ON_MS : PULSE_OFF_MS;
  if (now - lastToggleMs >= interval) {
    pulseOn = !pulseOn;
    lastToggleMs = now;
  }

  bool active = (currentState != STATE_IDLE) && pulseOn;
  digitalWrite(RED_PIN,   (currentState == STATE_WAITING && active) ? HIGH : LOW);
  digitalWrite(GREEN_PIN, (currentState == STATE_DONE    && active) ? HIGH : LOW);
}
