let x = 0, y = 0
let pitch = 0
let roll = 0

let calibrate = 0;

let rollMin = 0;
let rollMax = 0;
let rollRange = 0;

let pitchMin = 0;
let pitchMax = 0;
let pitchRange = 0;

function writeToSerial(code: number, data: string) {
    serial.writeString(`${code}:${data}\n`);
}

writeToSerial(0, "Init");

input.onButtonPressed(Button.B, function () {
    writeToSerial(1, "SHAKE");
})

input.onButtonPressed(Button.A, function () {
    if (calibrate == 0) {
        rollMin = input.rotation(Rotation.Roll) + 180
        calibrate++;
        writeToSerial(0, `RollMin: ${rollMin}`);
    } else if (calibrate == 1) {
        rollMax = input.rotation(Rotation.Roll) + 180
        rollRange = (rollMax - rollMin);
        writeToSerial(0, `RollMax: ${rollMax}, Range: ${rollRange}`)

        if (rollRange > 0) {
            calibrate++;
        }
    } else if (calibrate == 2) {
        pitchMin = input.rotation(Rotation.Pitch) + 180
        writeToSerial(0, `PitchMin: ${pitchMin}`)
        calibrate++;
    } else if (calibrate == 3) {
        pitchMax = input.rotation(Rotation.Pitch) + 180

        if (pitchMax < pitchMin) {
            roll = pitchMin;
            pitchMin = pitchMax;
            pitchMax = roll;
        }

        pitchRange = (pitchMax - pitchMin);
        writeToSerial(0, `PitchMax: ${pitchMax}, Range: ${pitchRange}`)

        if (pitchRange > 0) {
            calibrate++;
        }
    } else if (calibrate == 4) {
        calibrate++;
    } else if (calibrate == 5) {
        calibrate = 0;
    }
})

input.onGesture(Gesture.Shake, function () {
    writeToSerial(1, "SHAKE");
})

input.onGesture(Gesture.LogoUp, function () {
    writeToSerial(1, "LOGOUP");
})

input.onGesture(Gesture.LogoDown, function () {
    writeToSerial(1, "LOGODOWN");
})



basic.forever(function () {
    roll = input.rotation(Rotation.Roll) + 180
    pitch = input.rotation(Rotation.Pitch) + 180

    if (calibrate == 0) {

        basic.clearScreen()
        led.plot(0, 4)
        x = roll / 72
        y = pitch / 72
        led.plot(x, y)

    } else if (calibrate == 1) {

        basic.clearScreen()
        led.plot(1, 4)
        led.plot(0, 4)
        x = roll / 72
        y = pitch / 72
        led.plot(x, y)
    } else if (calibrate == 2) {
        basic.clearScreen()
        led.plot(0, 0)
        x = roll / 72
        y = pitch / 72
        led.plot(x, y)

    } else if (calibrate == 3) {
        basic.clearScreen()
        led.plot(1, 0)
        led.plot(0, 0)
        x = roll / 72
        y = pitch / 72
        led.plot(x, y)

    } else if (calibrate == 4 || calibrate == 5) {

        //serial.writeString("" + pitch.toString() + ":" + roll.toString() + "\n")
        roll = Math.floor(((Math.constrain(roll, rollMin, rollMax) - rollMin) / rollRange) * 360)
        pitch = Math.floor(((Math.constrain(pitch, pitchMin, pitchMax) - pitchMin) / pitchRange) * 360)

        x = Math.min(roll / 72, 4)
        y = Math.min(pitch / 72, 4)

        basic.clearScreen()
        led.plot(x, y)

        if (calibrate == 4) {
            led.plot(4, 4)
            led.plot(0, 0)
        }
    }

    if (calibrate == 5) {
        writeToSerial(2, `${roll}:${pitch}`)
    }
})
