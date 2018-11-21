function accessingWinRT(){
    // Make accessing the namespace more convenient in the code
    var WinRTComps = Wintellect.WinRTComponents;

    // NOTE: The JavaScript VM projects WinRT APIs via camel casing

    // Access WinRT type's static method & property
    // NOTE: JavaScript pass "null" here!
    var s = WinRTComps.WinRTClass.staticMethod(null); 
    var struct = {anumber:123, astring:"Jeff", aenum: WinRTComps.WinRTEnum.notNone};
    WinRTComps.WinRTClass.staticProperty = struct;
    s = WinRTComps.WinRTClass.staticProperty; // Read it back

    // If the method has out parameters, they and the return value
    // are returned as an object's properties
    var s = WinRTComps.WinRTClass.outParameters();
    var name = s.value; // Return value
    var struct = s.x; // an 'out' parameter
    var year = s.year; // another 'out' parameter

    // Construct an instance of the WinRT component
    var winRTClass = new WinRTComps.WinRTClass(null);
    s = winRTClass.toString(); // Call ToString()

    // Demonstrate throw and catch
    try { winRTClass.throwingMethod();}
    catch (err){}

    // Array passing
    var a = [1,2,3,4,5];
    var sum = winRTClass.passArray(a);

    // Array filling
    var arrayOut = [7,7,7]; // NOTE: fillArray sees all zeros!
    var length = winRTClass.fillArray(arrayOut); // On return, arrayOut = [0, 1, 2]

    // Array returning
    a = winRTClass.returnArray(); // a = [1,2,3]

    // Pass a collection and have its elements modified
    var localSettings = Windows.Storage.ApplicationData.current.localSettings;
    localSettings.values["Key1"] = "Value1";
    winRTClass.passAndModifyCollection(localSettings.values);
    // On return, localSettings.values has 2 key/value pairs in it

    // Call overloaded method
    winRTClass.someMethod(5); // Actually calls SomeMethod(String) passing "5"

    // Comsume the automatically implemented event
    var f = function(v) {return v.target;};
    winRTClass.addEventListener("autoevent", f, false);
    s = winRTClass.raiseAutoEvent(7);

    // Consume the manually implemented event
    winRTClass.addEventListener("manualevent", f, false);
    s = winRTClass.raiseManualEvent(8);

    // Invoke asynchronous method supporting progress, cancelation, & error handling
    var promise = winRTClass.doSomethingAsync();
    promise.then(
        function (result) { console.log("Async op complete: " + result);},
        function (error) { console.log("Async op error: " + error); },
        function (progress) {
            console.log("Async op progress: " + progress);
            // if (progress == 30) promise.cancel(); // To test cancelation
        });
}