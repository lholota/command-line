﻿using System;
using LH.CommandLine.Exceptions;
using LH.CommandLine.Options.Factoring;
using LH.CommandLine.Options.Metadata;
using LH.CommandLine.Options.Validation;
using LH.CommandLine.Options.Values;

namespace LH.CommandLine.Options
{
    public class OptionsParser<TOptions>
        where TOptions: class
    {
        private readonly OptionsMetadata _optionsMetadata;
        private readonly OptionsValidator _optionsValidator;
        private readonly OptionsFactory<TOptions> _optionsFactory;
        private readonly ValueParserSelector _valueParserSelector;
        private readonly OptionsMetadataValidator<TOptions> _optionsMetadataValidator;

        public OptionsParser(IValueParserFactory valueParserFactory)
        {
            _optionsMetadata = new OptionsMetadata(typeof(TOptions));
            _optionsFactory = new OptionsFactory<TOptions>(_optionsMetadata);
            _valueParserSelector = new ValueParserSelector(valueParserFactory);
            _optionsMetadataValidator = new OptionsMetadataValidator<TOptions>(_optionsMetadata, _optionsFactory, _valueParserSelector);
            _optionsValidator = new OptionsValidator();
        }

        public OptionsParser()
            : this(new ActivatorValueParserFactory())
        {
        }

        public TOptions Parse(string[] args)
        {
            _optionsMetadataValidator.Validate();

            OptionPropertyMetadata matchingProperty;

            var parsingContext = new OptionsParsingContext(_optionsMetadata);

            for (var i = 0; i < args.Length; i++)
            {
                if (_optionsMetadata.TryGetSwitchValueByName(args[i], out var switchValue))
                {
                    parsingContext.AddValue(switchValue.PropertyMetadata, switchValue.Value);
                    continue;
                }

                if (_optionsMetadata.TryGetPropertyByIndex(i, out matchingProperty))
                {
                    if (matchingProperty.IsCollection)
                    {
                        ParseAndSetCollectionValue(parsingContext, matchingProperty, args, ref i);
                    }
                    else
                    {
                        ParseAndSetValue(parsingContext, matchingProperty, args[i]);
                    }

                    continue;
                }

                if (i + 1 < args.Length)
                {
                    if (_optionsMetadata.TryGetPropertyByOptionName(args[i], out matchingProperty))
                    {
                        if (matchingProperty.IsCollection)
                        {
                            i++; // Skip the option name
                            ParseAndSetCollectionValue(parsingContext, matchingProperty, args, ref i);
                        }
                        else
                        {
                            ParseAndSetValue(parsingContext, matchingProperty, args[i + 1]);
                            i++;
                        }
                        continue;
                    }
                }

                parsingContext.AddInvalidOptionError(args[i]);
            }

            var options = _optionsFactory.CreateOptions(parsingContext.GetValues());
            var validationErrors = _optionsValidator.ValidateOptions(options);

            parsingContext.AddValidationErrors(validationErrors);

            if (parsingContext.HasErrors)
            {
                throw new InvalidOptionsException(parsingContext.Errors);
            }

            return options;
        }

        private void ParseAndSetCollectionValue(OptionsParsingContext context, OptionPropertyMetadata propertyMetadata, string[] args, ref int index)
        {
            var parser = _valueParserSelector.GetParserForProperty(propertyMetadata);

            while (index < args.Length)
            {
                if (_optionsMetadata.IsKeyword(args[index]))
                {
                    index--;
                    break;
                }

                try
                {
                    var parsedValue = parser.Parse(args[index], propertyMetadata.ParsedType);
                    context.AddValue(propertyMetadata, parsedValue);
                }
                catch (DuplicateValueException)
                {
                    context.AddSpecifiedMultipleTimesError(propertyMetadata);
                }
                catch (Exception)
                {
                    context.AddInvalidValueError(propertyMetadata, args[index]);
                }

                index++;
            }
        }

        private void ParseAndSetValue(OptionsParsingContext context, OptionPropertyMetadata propertyMetadata, string rawValue)
        {
            var parser = _valueParserSelector.GetParserForProperty(propertyMetadata);

            try
            {
                var parsedValue = parser.Parse(rawValue, propertyMetadata.Type);
                context.AddValue(propertyMetadata, parsedValue);
            }
            catch (DuplicateValueException)
            {
                context.AddSpecifiedMultipleTimesError(propertyMetadata);
            }
            catch (Exception)
            {
                context.AddInvalidValueError(propertyMetadata, rawValue);
            }
        }
    }
}