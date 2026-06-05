using ScriptLang.Runtime;

namespace ScriptLang.Prototype
{
    


    [PrototypeExtension]
    internal partial  class TestPersonPrototype
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is TestPerson;

        [PrototypeProperty]
        private StringValue Name(TestPerson person)
        {
            return new StringValue(person.Name);
        }

        [PrototypeProperty]
        private NumberValue<int> Age(TestPerson person)
        {
            return NumberValueFactory.Create(person.Age);
        }

        [PrototypeProperty]
        private ArrayValue Hobbies(TestPerson person)
        {
            return new ArrayValue(person.Hobbies.Select(x => (Value)new StringValue(x)).ToList());
        }

        [PrototypeFunction]
        public StringValue Greet(TestPerson person)
        {
            return new StringValue(person.Greet());
        }

        [PrototypeFunction]
        public NumberValue<int> AddYears(TestPerson person, NumberValue<int> years)
        {
            return NumberValueFactory.Create(person.AddYears(years.Value));
        }

        [PrototypeFunction]
        public void AddHobbies(TestPerson person, StringValue hobbie)
        {
            person.AddHobbies(hobbie.Value);
        }

        [PrototypeFunction]
        public void SetName(TestPerson person, StringValue name)
        {
            person.SetName(name.Value);
        }

    }
}
