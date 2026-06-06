using ScriptLang.Runtime;

namespace ScriptLang.Demo
{
    [PrototypeExtension]
    internal partial  class TestPersonPrototype
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is TestPerson;

        [PrototypeProperty]
        private static StringValue Name(TestPerson person)
        {
            return StringValue.Create(person.Name);
        }

        [PrototypeProperty]
        private static NumberValue<int> Age(TestPerson person)
        {
            return NumberValueFactory.Create(person.Age);
        }

        [PrototypeProperty]
        private static ArrayValue Hobbies(TestPerson person)
        {
            return new ArrayValue([.. person.Hobbies.Select(x => (Value)StringValue.Create(x))]);
        }

        [PrototypeFunction]
        public static StringValue Greet(TestPerson person)
        {
            return StringValue.Create(person.Greet());
        }

        [PrototypeFunction]
        public static NumberValue<int> AddYears(TestPerson person, NumberValue<int> years)
        {
            return NumberValueFactory.Create(person.AddYears(years.Value));
        }

        [PrototypeFunction]
        public static void AddHobbies(TestPerson person, StringValue hobbie)
        {
            person.AddHobbies(hobbie.Value);
        }

        [PrototypeFunction]
        public static void SetName(TestPerson person, StringValue name)
        {
            person.SetName(name.Value);
        }

    }
}
