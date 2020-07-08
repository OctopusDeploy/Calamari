if [diff <(jq -S . values.json) <(jq -S . expectedValues.json)]
then
  echo "different"
fi