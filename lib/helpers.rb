require File.join(File.dirname(__FILE__), 'breadcrumbs')

module Helpers
  def code(lang, &block)
    uv(:lang => lang, :theme => "twilight", :line_numbers => false, &block)
  end
end

Webby::Helpers.register(Helpers)