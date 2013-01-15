/// <reference path="jquery-1.9.0.js" />

var el = $("el").animate();
var test = el.css("b", "test");
test = el.animate({});


$("").click(function (e) {
    /// <param name="e" type="jQuery.Event">Description</param>
        
    
});

$("div").click(function (e) {
    
});

var i = el.height(100).width(100);



// Problem with declared return type not being carried across assignments
function A() {
    return new A.prototype.init();
}
A.prototype.init = function () {
    return this;
};
A.prototype.a = function () { // Hint & indeterminate execution
    /// <returns type="A" />
    return foo();
};
A.prototype.b = function () { // Hint only
    /// <returns type="A" />
}
A.prototype.c = function () { // Execution only
    return this;
};
A.prototype.d = function () { // Hint & execution
    /// <returns type="A" />
    return this;
};
A.prototype.e = function (p) { // Hint & conditional indeterminate execution
    /// <returns type="A" />
    return p ? this : foo();
};
A.prototype.f = function (p) { // Invalid hint only
    /// <returns type="A" type="A" />
};
A.prototype.g = function (p) { // Conditional return type, execution only
    return p ? "test" : 0;
}
A.prototype.h = function (p) { // Conditional return type, hint & execution
    /// <returns type="A" />
    return p ? this : "test";
}
A.prototype.z = function () {
    return null;
}

A.prototype.init.prototype = A.prototype;

var a = A().e("");


