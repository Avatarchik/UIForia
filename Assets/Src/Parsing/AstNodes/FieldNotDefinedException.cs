using System;

namespace Src {

    public class FieldNotDefinedException : Exception {

        public FieldNotDefinedException(Type type, string identifier) : base($"Type `{type.Name} does not have a field called `{identifier}`") { }

    }

}